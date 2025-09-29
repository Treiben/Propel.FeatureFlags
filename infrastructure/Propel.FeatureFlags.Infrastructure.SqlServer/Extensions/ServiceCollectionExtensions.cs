using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Infrastructure.SqlServer;

namespace Propel.FeatureFlags.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddFeatureFlagPersistence(this IServiceCollection services, string sqlServerConnectionString)
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

		services.AddSingleton<IFeatureFlagRepository>(sp =>
			new SqlFeatureFlagRepository(
				configuredConnectionString,
				sp.GetRequiredService<ILogger<SqlFeatureFlagRepository>>()));

		services.AddDatabaseInitializer(configuredConnectionString);

		return services;
	}

	public static IServiceCollection AddDatabaseInitializer(this IServiceCollection services, string connectionString)
	{
		services.AddSingleton(sp =>
			new SqlDatabaseInitializer(connectionString, sp.GetRequiredService<ILogger<SqlDatabaseInitializer>>()));
		return services;
	}

	/// <summary>
	/// Ensures the SQL Server database and schema exist for feature flags
	/// Call this during application startup
	/// </summary>
	public static async Task<IServiceProvider> EnsureFeatureFlagDatabase(this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var initializer = services.GetRequiredService<SqlDatabaseInitializer>();
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
	public static async Task<IServiceProvider> SeedFeatureFlags(this IServiceProvider services, string file)
	{
		var initializer = services.GetRequiredService<SqlDatabaseInitializer>();
		var seeded = await initializer.SeedAsync(file);
		if (!seeded)
			throw new InvalidOperationException("Failed to seed SQL Server database for feature flags");
		return services;
	}
}