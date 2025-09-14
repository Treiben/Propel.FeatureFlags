using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPostgresSqlFeatureFlags(this IServiceCollection services, string connectionString)
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
			ConnectionPruningInterval = 10
		};

		var configuredConnectionString = builder.ToString();

		services.AddSingleton<IFeatureFlagRepository>(sp =>
			new PostgreSQLFeatureFlagRepository(
				configuredConnectionString,
				sp.GetRequiredService<ILogger<PostgreSQLFeatureFlagRepository>>()));

		services.AddSingleton(sp =>
			new PostgreSQLDatabaseInitializer(
				connectionString ?? throw new InvalidOperationException("PostgreSQL connection string required"),
				sp.GetRequiredService<ILogger<PostgreSQLDatabaseInitializer>>()));

		return services;
	}

	/// <summary>
	/// Ensures the PostgreSQL database and schema exist for feature flags
	/// Call this during application startup
	/// </summary>
	public static async Task<IServiceProvider> EnsureFeatureFlagsDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
	{
		var initializer = services.GetRequiredService<PostgreSQLDatabaseInitializer>();
		var initialized = await initializer.InitializeAsync(cancellationToken);
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize PostgreSQL database for feature flags");

		return services;
	}
}
