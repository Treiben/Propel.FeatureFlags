using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.SqlServer.Extensions;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Adds SQL Server-based feature flag support to the specified <see cref="IServiceCollection"/>.
	/// </summary>
	/// <remarks>This method registers the necessary services for using SQL Server as the backing store for feature
	/// flags.  It configures a singleton instance of <see cref="IFeatureFlagRepository"/> that interacts with the database
	/// and ensures the database is initialized for feature flag storage.</remarks>
	/// <param name="services">The <see cref="IServiceCollection"/> to which the feature flag services will be added.</param>
	/// <param name="connectionString">The connection string used to connect to the SQL Server database. This connection string is internally configured 
	/// with resilience settings such as connection pooling and timeouts.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	public static IServiceCollection AddSqlServerFeatureFlags(this IServiceCollection services, string connectionString)
	{
		// Configure connection string with resilience settings
		var builder = new SqlConnectionStringBuilder(connectionString)
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

	internal static IServiceCollection AddDatabaseInitializer(this IServiceCollection services, string connectionString)
	{
		services.TryAddSingleton(sp =>
			new SqlDatabaseInitializer(connectionString, sp.GetRequiredService<ILogger<SqlDatabaseInitializer>>()));
		return services;
	}

	internal static async Task<IServiceProvider> EnsureFeatureFlagDatabase(this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var initializer = services.GetRequiredService<SqlDatabaseInitializer>();
		var initialized = await initializer.InitializeAsync(cancellationToken);
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize SQL Server database for feature flags");
		return services;
	}
}