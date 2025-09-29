using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations.CLI.Cli;
using Propel.FeatureFlags.Migrations.CLI.Providers;
using Propel.FeatureFlags.Migrations.CLI.Services;
using System.CommandLine;

namespace DbMigrationCli;

class Program
{
	static async Task<int> Main(string[] args)
	{
		var builder = Host.CreateApplicationBuilder(args);

		// Configure services
		builder.Services.AddLogging(configure => configure.AddConsole());
		builder.Services.AddTransient<IDatabaseProviderFactory, DatabaseProviderFactory>();
		builder.Services.AddTransient<IMigrationService, MigrationService>();
		builder.Services.AddTransient<ISeedService, SeedService>();

		var host = builder.Build();

		// Create root command
		var rootCommand = new RootCommand("Database Migration CLI");

		// Common options
		var connectionStringOption = new Option<string>("--connection-string")
		{
			Description = "Database connection string",
			Required = true
		};

		var providerOption = new Option<string>("--provider")
		{
			Description = "Database provider (sqlserver or postgresql)",
			Required = true
		};

		var migrationsPathOption = new Option<string>("--migrations-path")
		{
			Description = "Path to migrations directory",
			Required = false,
			DefaultValueFactory = (a) => "./Migrations"
		};

		var baseline = new BaselineCommand(host.Services).Build(connectionStringOption, providerOption);
		var migrate = new MigrateCommand(host.Services).Build(connectionStringOption, providerOption, migrationsPathOption);
		var rollback = new RollbackCommand(host.Services).Build(connectionStringOption, providerOption, migrationsPathOption);
		var seed = new SeedCommand(host.Services).Build(connectionStringOption, providerOption);
		var status = new StatusCommand(host.Services).Build(connectionStringOption, providerOption, migrationsPathOption);

		// Add commands to root
		rootCommand.Add(baseline);
		rootCommand.Add(migrate);
		rootCommand.Add(rollback);
		rootCommand.Add(seed);
		rootCommand.Add(status);

		ParseResult parseResult = rootCommand.Parse(args);
		return parseResult.Invoke();
	}





}