using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.PostgreSql.Extensions;

internal static class DatabaseExtensions
{
	public static IServiceCollection AddFeatureFlagRepository(this IServiceCollection services, string connectionString)
	{
		// Configure connection string with resilience settings here
		var builder = new NpgsqlConnectionStringBuilder(connectionString)
		{
			CommandTimeout = 30,
			Timeout = 15,
			MaxPoolSize = 100,
			MinPoolSize = 5,
			Pooling = true,
			ConnectionIdleLifetime = 300,
			ConnectionPruningInterval = 10,
			ApplicationName = "PropelFeatureFlags"
		};

		var configuredConnectionString = builder.ToString();

		services.AddSingleton<IFeatureFlagRepository>(sp =>
			new PostgresFeatureFlagRepository(
				configuredConnectionString,
				sp.GetRequiredService<ILogger<PostgresFeatureFlagRepository>>()));

		services.AddDatabaseInitializer(configuredConnectionString);

		return services;
	}

	public static IServiceCollection AddDatabaseInitializer(this IServiceCollection services, string connectionString)
	{
		services.TryAddSingleton(sp =>
			new PostgresDatabaseInitializer(connectionString, sp.GetRequiredService<ILogger<PostgresDatabaseInitializer>>()));
		return services;
	}

	/// <summary>
	/// Ensures the PostgreSQL database and schema exist for feature flags
	/// Call this during application startup
	/// </summary>
	public static async Task<IServiceProvider> EnsureFeatureFlagDatabase(this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var initializer = services.GetRequiredService<PostgresDatabaseInitializer>();
		var initialized = await initializer.InitializeAsync(cancellationToken);
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize PostgreSQL database for feature flags");
		return services;
	}

	/// <summary>
	/// Ensures the PostgreSQL database is seeded with initial data for feature flags
	/// Flags should be defined in the provided SQL script file
	/// Use: during application startup at development time
	/// For production, use migrations instead or seed with registered flags from assembly
	/// </summary>
	public static async Task<IServiceProvider> SeedFeatureFlags(this IServiceProvider services, string file)
	{
		var initializer = services.GetRequiredService<PostgresDatabaseInitializer>();
		var seeded = await initializer.SeedAsync(file);
		if (!seeded)
			throw new InvalidOperationException("Failed to seed PostgreSQL database for feature flags");
		return services;
	}
}
