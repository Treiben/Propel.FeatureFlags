using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Propel.FeatureFlags.Migrations;

public class MigrationRunner(IServiceProvider services, IHostApplicationLifetime lifetime, ILogger<MigrationRunner> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		using var serviceScope = services.CreateScope();
		var migrator = serviceScope.ServiceProvider.GetRequiredService<Migrator>();
		var logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<MigrationRunner>>();

		try
		{
			Console.WriteLine("Type --help to see the options or type --exit to exit the CLI");

			await migrator.Run();

			logger.LogInformation("Migration completed successfully.");

			Console.WriteLine("Migration completed successfully.");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Migration failed: {Message}", ex.Message);

			Console.WriteLine("Migration failed. Check logs for details.");
		}
		finally
		{
			// Stop the application once the migration is done
			lifetime.StopApplication();
		}
	}
	public Task StopAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("MigrationRunner is stopping.");
		return Task.CompletedTask;
	}
}

public class Migrator(IMigrationEngine migrationEngine, IConfiguration configuration, ILogger<Migrator> logger)
{
	public async Task Run(params string[] args)
	{
		try
		{
			logger.LogInformation("Starting Propel Feature Flags Database Migration");

			var exit = configuration["exit"];
			if (!string.IsNullOrWhiteSpace(exit))
			{
				logger.LogInformation("Exit flag is set to true. Exiting without performing any migration.");
				return;
			}

			var result = await ExecuteMigrationAsync();
			if (result != MigrationResult.Pass())
				result = await ExecuteSeedAsync();
			if (result != MigrationResult.Pass())
				result = await ExecuteRollbackAsync();
			if (result != MigrationResult.Pass())
				result = await SetBaselineAsync();
			if (result != MigrationResult.Pass())
				result = await ValidateAsync();
			if (result != MigrationResult.Pass())
				result = await ShowStatusAsync();
			if (result != MigrationResult.Pass())
				result = ShowHelp();
			

			Console.WriteLine(result.ShowMigrationResultStatus());

			Console.WriteLine();
			Console.WriteLine("Type --exit to exit application");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Migration failed: {Message}", ex.Message);
			return;
		}
	}

	private async Task<MigrationResult?> ExecuteMigrationAsync()
	{
		var command = configuration["migrate"];

		if (string.IsNullOrWhiteSpace(command))
			command = configuration["up"];
		if (string.IsNullOrWhiteSpace(command))
			return MigrationResult.Pass();

		logger.LogInformation("Received command {Command}", command);

		Console.WriteLine($"Migrating schema...");
		return await migrationEngine.MigrateAsync();
	}

	private async Task<MigrationResult?> ExecuteSeedAsync()
	{
		var command = configuration["seed"];
		if (string.IsNullOrWhiteSpace(command))
			return MigrationResult.Pass();

		logger.LogInformation("Received command {Command}", command);

		Console.WriteLine($"Seeding data from script...");
		return await migrationEngine.SeedAsync();
	}

	private async Task<MigrationResult?> ExecuteRollbackAsync()
	{
		var command = configuration["rollback"];
		if (string.IsNullOrWhiteSpace(command))
			command = configuration["down"];
		if (string.IsNullOrWhiteSpace(command))
			return MigrationResult.Pass();

		logger.LogInformation("Received command {Command}", command);

		Console.WriteLine($"Rolling back migration...");
		return await migrationEngine.RollbackAsync();
	}

	private async Task<MigrationResult?> SetBaselineAsync()
	{
		var command = configuration["baseline"];
		if (string.IsNullOrWhiteSpace(command))
			return MigrationResult.Pass();

		logger.LogInformation("Received command {Command}", command);

		Console.WriteLine($"Setting up version baseline...");
		return await migrationEngine.BaselineAsync();
	}

	private async Task<MigrationResult?> ShowStatusAsync()
	{
		var command = configuration["status"];
		if (string.IsNullOrWhiteSpace(command))
			return MigrationResult.Pass();

		logger.LogInformation("Received command {Command}", command);

		Console.WriteLine($"Fetching migration status...");
		await migrationEngine.ShowStatusAsync();
		return MigrationResult.Ok();
	}

	private async Task<MigrationResult?> ValidateAsync()
	{
		var command = configuration["validate"];
		if (string.IsNullOrWhiteSpace(command))
			return MigrationResult.Pass();

		logger.LogInformation("Received command {Command}", command);

		Console.WriteLine($"Validating migration files...");
		return await migrationEngine.ValidateAsync();
	}

	private MigrationResult ShowHelp()
	{
		var command = configuration["help"];
		if (string.IsNullOrWhiteSpace(command))
			return MigrationResult.Pass();

		logger.LogInformation("Received command {Command}", command);

		ShowUsage();
		return MigrationResult.Ok();
	}

	static void ShowUsage()
	{
		Console.WriteLine(@"
Propel Feature Flags Database Migration Tool

Usage: dotnet run [command] [options]

Commands:
  migrate, up       Apply pending migrations (default)
  rollback, down    Rollback migrations (specify number of steps)
  seed				Seed initial data
  status, info      Show migration status
  validate          Validate migration files
  baseline          Mark database as baseline version
  help				Show this help message

Examples:
  dotnet run --migrate -p 'scripts/migrations'
  dotnet run --rollback -p 'scripts/rollbacks' -s 2
  dotnet run --seed -p 'scripts/seed.sql'
  dotnet run --status
  dotnet run --baseline

Environment Variables:
  PROPEL_CONNECTION_STRING    Override connection string
  PROPEL_ENVIRONMENT         Set environment (dev, staging, prod)
");
	}
}
