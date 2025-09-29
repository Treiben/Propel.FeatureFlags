using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations.CLI.Services;
using System.CommandLine;

namespace Propel.FeatureFlags.Migrations.CLI.Cli;

public class MigrateCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MigrateCommand> _logger;

    public MigrateCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<MigrateCommand>>();
    }

    public async Task ExecuteAsync(string connectionString, string provider, string migrationsPath, string? targetVersion = null)
    {
        try
        {
            _logger.LogInformation("Starting database migration...");
            _logger.LogInformation("Provider: {Provider}", provider);
            _logger.LogInformation("Migrations Path: {MigrationsPath}", migrationsPath);
            
            if (!string.IsNullOrEmpty(targetVersion))
            {
                _logger.LogInformation("Target Version: {TargetVersion}", targetVersion);
            }

            var migrationService = _serviceProvider.GetRequiredService<IMigrationService>();
            await migrationService.MigrateAsync(connectionString, provider, migrationsPath, targetVersion);
            
            _logger.LogInformation("Migration completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed: {Message}", ex.Message);
            Environment.Exit(1);
        }
    }

	public Command Build(Option<string> connectionStringOption,
					     Option<string> providerOption,
					     Option<string> migrationsPathOption)
	{
		// Migrate command
		var migrateCommand = new Command("migrate", "Run database migrations");

		migrateCommand.Options.Add(connectionStringOption);
		migrateCommand.Options.Add(providerOption);
		migrateCommand.Options.Add(migrationsPathOption);

		var targetVersionOption = new Option<string?>("--target-version")
		{
			Description = "Target version to migrate to (optional)",
			Required = false,
			DefaultValueFactory = (a) => null
		};
		migrateCommand.Options.Add(targetVersionOption);

		migrateCommand.SetAction(async (parseResult) =>
		{
			var connectionString = parseResult.GetValue(connectionStringOption);
			var provider = parseResult.GetValue(providerOption);
			var migrationsPath = parseResult.GetValue(migrationsPathOption);
			var targetVersion = parseResult.GetValue(targetVersionOption);

			await ExecuteAsync(connectionString, provider, migrationsPath, targetVersion);
		});
		return migrateCommand;
	}
}