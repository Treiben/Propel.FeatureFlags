using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations.CLI.Services;
using System.CommandLine;

namespace Propel.FeatureFlags.Migrations.CLI.Cli;

public class RollbackCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RollbackCommand> _logger;

    public RollbackCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<RollbackCommand>>();
    }

    public async Task ExecuteAsync(string connectionString, string provider, string migrationsPath, string version)
    {
        try
        {
            _logger.LogInformation("Starting database rollback...");
            _logger.LogInformation("Provider: {Provider}", provider);
            _logger.LogInformation("Migrations Path: {MigrationsPath}", migrationsPath);
            _logger.LogInformation("Target Version: {Version}", version);

            var migrationService = _serviceProvider.GetRequiredService<IMigrationService>();
            await migrationService.RollbackAsync(connectionString, provider, migrationsPath, version);
            
            _logger.LogInformation("Rollback completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed: {Message}", ex.Message);
            Environment.Exit(1);
        }
    }

	public Command Build(Option<string> connectionStringOption,
						Option<string> providerOption,
						Option<string> migrationsPathOption)
	{
		// Rollback command
		var rollbackCommand = new Command("rollback", "Rollback database migrations");
		rollbackCommand.Options.Add(connectionStringOption);
		rollbackCommand.Options.Add(providerOption);
		rollbackCommand.Options.Add(migrationsPathOption);

		var rollbackVersionOption = new Option<string>("--version")
		{
			Description = "Version to rollback to",
			Required = true
		};
		rollbackCommand.Options.Add(rollbackVersionOption);

		rollbackCommand.SetAction(async (parseResult) =>
		{
			var connectionString = parseResult.GetValue(connectionStringOption);
			var provider = parseResult.GetValue(providerOption);
			var migrationsPath = parseResult.GetValue(migrationsPathOption);
			var version = parseResult.GetValue(rollbackVersionOption);

			await ExecuteAsync(connectionString, provider, migrationsPath, version);
		});

		return rollbackCommand;
	}
}