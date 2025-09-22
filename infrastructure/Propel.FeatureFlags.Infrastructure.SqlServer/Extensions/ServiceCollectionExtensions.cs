using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations;

namespace Propel.FeatureFlags.Infrastructure.SqlServer.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPropelPersistence(this IServiceCollection services, string sqlServerConnectionString)
	{
		// Configure connection string with resilience settings
		var builder = new SqlConnectionStringBuilder(sqlServerConnectionString)
		{
			CommandTimeout = 30,
			ConnectTimeout = 15,
			MaxPoolSize = 100,
			MinPoolSize = 5,
			Pooling = true,
			ApplicationName = "PropelFeatureFlags"
		};

		var configuredConnectionString = builder.ToString();

		services.AddSingleton<IFlagEvaluationRepository>(sp =>
			new ClientApplicationRepository(
				configuredConnectionString,
				sp.GetRequiredService<ILogger<ClientApplicationRepository>>()));

		services.AddSingleton(sp =>
			new SqlServerDatabaseInitializer(
				sqlServerConnectionString ?? throw new InvalidOperationException("SQL Server connection string required"),
				sp.GetRequiredService<ILogger<SqlServerDatabaseInitializer>>()));

		return services;
	}

	public static IServiceCollection AddMigrationServices(this IServiceCollection services, IConfiguration configuration)
	{
		var optionsSection = configuration.GetSection("SqlMigrationOptions");
		var options = optionsSection.Get<SqlMigrationOptions>();

		services.AddSingleton(options);
		services.AddSingleton<IMigrationEngine, MigrationEngine>();
		services.AddSingleton<IMigrationRepository, SqlServerMigrationRepository>();
		services.AddSingleton<Migrator>();

		return services;
	}

	/// <summary>
	/// Ensures the SQL Server database and schema exist for feature flags
	/// Call this during application startup
	/// </summary>
	public static async Task<IServiceProvider> EnsurePropelDatabase(this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var initializer = services.GetRequiredService<SqlServerDatabaseInitializer>();
		var initialized = await initializer.InitializeAsync(cancellationToken);
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize SQL Server database for feature flags");
		return services;
	}

	/// <summary>
	/// Ensures the SQL Server database is seeded with initial data for feature flags
	/// Flags should be defined in the provided SQL script file
	/// Use: during application startup at development time
	/// For production, use migrations instead or seed with registered flags from assembly
	/// </summary>
	public static async Task<IServiceProvider> SeedDatabaseAsync(this IServiceProvider services, string sqlScriptFile)
	{
		var initializer = services.GetRequiredService<SqlServerDatabaseInitializer>();
		var seeded = await initializer.SeedAsync(sqlScriptFile);
		if (!seeded)
			throw new InvalidOperationException("Failed to seed SQL Server database for feature flags");
		return services;
	}
}