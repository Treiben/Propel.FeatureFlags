using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.PostgreSql.Extensions;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds PostgreSQL-based feature flag support to the specified <see cref="IServiceCollection"/>.
	/// </summary>
	/// <remarks>This method registers the necessary services for using PostgreSQL as the backend for feature flag
	/// storage. It configures the connection string with optimized settings for resilience and performance, and registers
	/// the <see cref="IFeatureFlagRepository"/> implementation as a singleton. Additionally, it ensures that the database
	/// initializer is added to the service collection.</remarks>
	/// <param name="services">The <see cref="IServiceCollection"/> to which the feature flag services will be added.</param>
	/// <param name="connectionString">The connection string used to connect to the PostgreSQL database. This connection string will be configured with
	/// resilience settings such as pooling and timeouts.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	public static IServiceCollection AddPostgreSqlFeatureFlags(this IServiceCollection services, string connectionString)
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

	internal static IServiceCollection AddDatabaseInitializer(this IServiceCollection services, string connectionString)
	{
		services.TryAddSingleton(sp =>
			new PostgresDatabaseInitializer(connectionString, sp.GetRequiredService<ILogger<PostgresDatabaseInitializer>>()));
		return services;
	}

	internal static async Task<IServiceProvider> EnsureFeatureFlagDatabase(this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var initializer = services.GetRequiredService<PostgresDatabaseInitializer>();
		var initialized = await initializer.InitializeAsync(cancellationToken);
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize PostgreSQL database for feature flags");
		return services;
	}
}
