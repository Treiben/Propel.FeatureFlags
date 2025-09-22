using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Propel.FeatureFlags.Migrations;

public class Migrator(IMigrationEngine migrationEngine, IConfiguration configuration, ILogger<Migrator> logger)
{
	public async Task<MigrationResult> Run(params string[] args)
	{
		try
		{
			logger.LogInformation("Starting Propel Feature Flags Database Migration");

			var command = CommandFromConfig();

			logger.LogInformation("Received command {Command}", command);

			switch (command)
			{
				case "migrate":
					return await migrationEngine.MigrateAsync();

				case "rollback":
					return await migrationEngine.RollbackAsync();

				case "status":
					 await migrationEngine.ShowStatusAsync();
					return MigrationResult.Ok();

				case "validate":
					return await migrationEngine.ValidateAsync();

				case "baseline":
					return await migrationEngine.BaselineAsync();

				default:
					ShowUsage();
					return MigrationResult.Ok();
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Migration failed: {Message}", ex.Message);
			return MigrationResult.Failed(because: ex.Message);
		}
	}

	private string CommandFromConfig()
	{
		var command = configuration["command"];

		if (string.IsNullOrWhiteSpace(command))
		{
			return "show-usage";
		}

		command = command.ToLowerInvariant();
		if (command == "migrate" || command == "up")
		{
			return "migrate";
		}

		if (command == "rollback" || command == "down")
		{
			return "rollback";
		}

		if (command == "status" || command == "info")
		{
			return "status";
		}

		if (command == "validate")
		{
			return "validate";
		}

		if (command == "baseline")
		{
			return "baseline";
		}

		return "show-usage";
	}

	static void ShowUsage()
	{
		Console.WriteLine(@"
Propel Feature Flags Database Migration Tool

Usage: dotnet run [command] [options]

Commands:
  migrate, up       Apply pending migrations (default)
  rollback, down    Rollback migrations (specify number of steps)
  status, info      Show migration status
  validate          Validate migration files
  baseline          Mark database as baseline version

Examples:
  dotnet run migrate
  dotnet run rollback 2
  dotnet run status
  dotnet run baseline

Environment Variables:
  PROPEL_CONNECTION_STRING    Override connection string
  PROPEL_ENVIRONMENT         Set environment (dev, staging, prod)
");
	}
}
