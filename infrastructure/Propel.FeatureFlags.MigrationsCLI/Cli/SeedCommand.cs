using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.MigrationsCLI.Services;
using System.CommandLine;

namespace Propel.FeatureFlags.MigrationsCLI.Cli;

public class SeedCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeedCommand> _logger;

    public SeedCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<SeedCommand>>();
    }

    public async Task ExecuteAsync(string connectionString, string provider, string seedsPath)
    {
        try
        {
            _logger.LogInformation("Starting database seeding...");
            _logger.LogInformation("Provider: {Provider}", provider);
            _logger.LogInformation("Seeds Path: {SeedsPath}", seedsPath);

            var seedService = _serviceProvider.GetRequiredService<ISeedService>();
            await seedService.SeedAsync(connectionString, provider, seedsPath);
            
            _logger.LogInformation("Seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding failed: {Message}", ex.Message);
            Environment.Exit(1);
        }
    }

	public Command Build(Option<string> connectionStringOption, Option<string> providerOption)
	{
		// Seed command
		var seedCommand = new Command("seed", "Run database seeds");
		seedCommand.Options.Add(connectionStringOption);
		seedCommand.Options.Add(providerOption);

		var seedsPathOption = new Option<string>(name: "--seeds-path")
		{
			Description = "Path to seeds directory",
			Required = false,
			DefaultValueFactory = (a) => "./Seeds"
		};

		seedCommand.Options.Add(seedsPathOption);

		seedCommand.SetAction(async (parseResult) =>
		{
			var connectionString = parseResult.GetValue(connectionStringOption);
			var provider = parseResult.GetValue(providerOption);
			var seedsPath = parseResult.GetValue(seedsPathOption);

			await ExecuteAsync(connectionString, provider, seedsPath);
		});

		return seedCommand;
	}
}