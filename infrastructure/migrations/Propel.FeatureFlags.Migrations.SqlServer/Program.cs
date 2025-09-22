using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations;
using Propel.FeatureFlags.Migrations.SqlServer;

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


public class MigrationRunner(IServiceProvider services, IHostApplicationLifetime lifetime, ILogger<MigrationRunner> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		using var serviceScope = services.CreateScope();
		var migrator = serviceScope.ServiceProvider.GetRequiredService<Migrator>();
		var logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

		try
		{
			await migrator.Run();

			logger.LogInformation("Migration completed successfully.");

			Console.WriteLine("Migration completed successfully.");
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Migration failed: {Message}", ex.Message);

			Console.WriteLine("Migration failed. Check logs for details.");
		}
		finally 		{
			// Stop the application once the migration is done
			lifetime.StopApplication();
		}
	}
	public Task StopAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("MigrationRunner is stopping.");
		return Task.CompletedTask;
	}
}



