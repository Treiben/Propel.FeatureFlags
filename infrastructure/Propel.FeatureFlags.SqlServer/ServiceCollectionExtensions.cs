using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.SqlServer;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddSqlServerFeatureFlags(this IServiceCollection services, FeatureFlagConfigurationOptions options)
	{
		services.AddSingleton<IFeatureFlagRepository>(sp =>
			new SqlServerFeatureFlagRepository(
				options.SqlConnectionString ?? throw new InvalidOperationException("SqlServer connection string required"),
				sp.GetRequiredService<ILogger<SqlServerFeatureFlagRepository>>()));

		services.AddSingleton(sp =>
			new SqlServerDatabaseInitializer(
		options.SqlConnectionString ?? throw new InvalidOperationException("SqlServer connection string required"),
		sp.GetRequiredService<ILogger<SqlServerDatabaseInitializer>>()));

		return services;
	}

	/// <summary>
	/// Ensures the SQL Server database and schema exist for feature flags
	/// Call this during application startup
	/// </summary>
	public static async Task<IServiceProvider> EnsureFeatureFlagsDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
	{
		var initializer = services.GetRequiredService<SqlServerDatabaseInitializer>();
		var initialized = await initializer.InitializeAsync(cancellationToken);
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize PostgreSQL database for feature flags");

		return services;
	}
}
