using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Infrastructure.SqlServer.Extensions;
using Propel.FeatureFlags.Migrations;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
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
	.AddMigrationServices(builder.Configuration)
	.BuildServiceProvider();

// Register your application's services here

builder.Services.AddHostedService<MigrationRunner>();

var host = builder.Build();

try
{
	await host.RunAsync();
	return 0;
}
catch (Exception ex)
{
		return 1;
}
finally
{
	if (host is IAsyncDisposable asyncDisposable)
	{
		await asyncDisposable.DisposeAsync();
	}
	else
	{
		host.Dispose();
	}
}


