using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations;
using Propel.FeatureFlags.Migrations.SqlServer;

var configuration = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: false)
	.AddEnvironmentVariables()
	.AddCommandLine(args)
	.Build();

var services = new ServiceCollection()
	.AddSingleton<IConfiguration>(configuration)
	.AddLogging(builder => 
	{
		builder
			.AddConsole()
			.SetMinimumLevel(LogLevel.Information);
	
		// Ensure console provider is configured properly
		builder.AddSimpleConsole(options =>
		{
			options.IncludeScopes = false;
			options.SingleLine = true;
			options.TimestampFormat = "HH:mm:ss ";
		});
	})
	.AddMigrationServices()
	.BuildServiceProvider();

using var serviceScope = services.CreateScope();
var migrator = serviceScope.ServiceProvider.GetRequiredService<Migrator>();

await migrator.Run(args);


