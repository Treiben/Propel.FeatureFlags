using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.MigrationsCLI.Services;
using System.CommandLine;

namespace Propel.FeatureFlags.MigrationsCLI.Cli;

public class StatusCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StatusCommand> _logger;

    public StatusCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<StatusCommand>>();
    }

    public async Task ExecuteAsync(string connectionString, string provider, string migrationsPath)
    {
        try
        {
            _logger.LogInformation("Checking migration status...");
            _logger.LogInformation("Provider: {Provider}", provider);
            _logger.LogInformation("Migrations Path: {MigrationsPath}", migrationsPath);

            var migrationService = _serviceProvider.GetRequiredService<IMigrationService>();
            var status = await migrationService.GetStatusAsync(connectionString, provider, migrationsPath);
            
            Console.WriteLine("\nMigration Status:");
            Console.WriteLine("================");
            
            if (!status.Any())
            {
                Console.WriteLine("No migrations found.");
                return;
            }

            Console.WriteLine($"{"Version",-20} {"Status",-10} {"Applied At",-20} {"Description"}");
            Console.WriteLine(new string('-', 80));
            
            foreach (var migration in status.OrderBy(m => m.Version))
            {
                var appliedAt = migration.AppliedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not Applied";
                var status_text = migration.IsApplied ? "Applied" : "Pending";
                Console.WriteLine($"{migration.Version,-20} {status_text,-10} {appliedAt,-20} {migration.Description}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status check failed: {Message}", ex.Message);
            Environment.Exit(1);
        }
    }

	public Command Build(Option<string> connectionStringOption,
					     Option<string> providerOption,
					     Option<string> migrationsPathOption)
	{
		// Status command
		var statusCommand = new Command("status", "Show migration status");
		statusCommand.Options.Add(connectionStringOption);
		statusCommand.Options.Add(providerOption);
		statusCommand.Options.Add(migrationsPathOption);

		statusCommand.SetAction(async (parseResult) =>
		{
			var connectionString = parseResult.GetValue(connectionStringOption);
			var provider = parseResult.GetValue(providerOption);
			var migrationsPath = parseResult.GetValue(migrationsPathOption);

			await ExecuteAsync(connectionString, provider, migrationsPath);
		});

		return statusCommand;
	}
}