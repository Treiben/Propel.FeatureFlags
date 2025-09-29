using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations.CLI.Services;
using System.CommandLine;

namespace Propel.FeatureFlags.Migrations.CLI.Cli;

public class BaselineCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BaselineCommand> _logger;

    public BaselineCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<BaselineCommand>>();
    }

    public async Task ExecuteAsync(string connectionString, string provider, string version)
    {
        try
        {
            _logger.LogInformation("Creating baseline migration entry...");
            _logger.LogInformation("Provider: {Provider}", provider);
            _logger.LogInformation("Baseline Version: {Version}", version);

            var migrationService = _serviceProvider.GetRequiredService<IMigrationService>();
            await migrationService.BaselineAsync(connectionString, provider, version);
            
            _logger.LogInformation("Baseline created successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Baseline creation failed: {Message}", ex.Message);
            Environment.Exit(1);
        }
    }

    public Command Build(Option<string> connectionStringOption, 
                         Option<string> providerOption)
	{
		// Baseline command
		var baselineCommand = new Command("baseline", "Create baseline migration entry");
		baselineCommand.Options.Add(connectionStringOption);
		baselineCommand.Options.Add(providerOption);

		var baselineVersionOption = new Option<string>("--version")
		{
			Description = "Baseline version",
			Required = true
		};
		baselineCommand.Options.Add(baselineVersionOption);

		baselineCommand.SetAction(async (parseResult) =>
		{
			var connectionString = parseResult.GetValue(connectionStringOption);
			var provider = parseResult.GetValue(providerOption);
			var version = parseResult.GetValue(baselineVersionOption);

            await ExecuteAsync(connectionString, provider, version);
		});

		return baselineCommand;
	}
}