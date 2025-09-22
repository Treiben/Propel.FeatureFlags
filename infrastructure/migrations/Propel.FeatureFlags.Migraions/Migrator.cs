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
					var rollbackParts = configuration["action"].Split(':');
					var steps = rollbackParts.Length > 1 && int.TryParse(rollbackParts[1], out var s) ? s : 1;
					return await migrationEngine.RollbackAsync(steps);

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
		var action = configuration["action"];

		Console.WriteLine($"Action from config: '{action}'");

		if (string.IsNullOrWhiteSpace(action))
		{
			return "show-usage";
		}

		action = action.ToLowerInvariant();
		if (action == "migrate" || action == "up")
		{
			return "migrate";
		}

		if (action.StartsWith("rollback") || action.StartsWith("down"))
		{
			return "rollback";
		}

		if (action == "status" || action == "info")
		{
			return "status";
		}

		if (action == "validate")
		{
			return "validate";
		}

		if (action == "baseline")
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
  dotnet run rollback:2
  dotnet run status
  dotnet run baseline

Environment Variables:
  PROPEL_CONNECTION_STRING    Override connection string
  PROPEL_ENVIRONMENT         Set environment (dev, staging, prod)
");
	}
}
