using Microsoft.Extensions.Logging;

namespace Propel.FeatureFlags.Migrations;

public class Migrator(IMigrationEngine migrationEngine, ILogger<Migrator> logger)
{
	public async Task<MigrationResult> Run(string[] args)
	{
		try
		{
			logger.LogInformation("Starting Propel Feature Flags Database Migration");

			var command = args.Length > 0 ? args[0].ToLower() : "migrate";

			switch (command)
			{
				case "migrate":
				case "up":
					return await migrationEngine.MigrateAsync();

				case "rollback":
				case "down":
					var steps = args.Length > 1 && int.TryParse(args[1], out var s) ? s : 1;
					return await migrationEngine.RollbackAsync(steps);

				case "status":
				case "info":
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
