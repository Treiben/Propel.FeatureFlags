using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;

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

		services.AddSingleton<IFlagManagementRepository>(sp =>
			new FlagManagementRepository(
				configuredConnectionString,
				sp.GetRequiredService<ILogger<FlagManagementRepository>>()));

		services.AddSingleton<IFlagEvaluationRepository>(sp =>
			new FlagEvaluationRepository(
				configuredConnectionString,
				sp.GetRequiredService<ILogger<FlagEvaluationRepository>>()));

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
	public static async Task<IServiceProvider> EnsureFeatureFlagsDatabaseAsync(this IServiceProvider services,
		CancellationToken cancellationToken = default)
	{
		var initializer = services.GetRequiredService<PostgreSQLDatabaseInitializer>();
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
	public static async Task<IServiceProvider> SeedDatabaseFromScriptAsync(this IServiceProvider services, string sqlScriptFile)
	{
		var initializer = services.GetRequiredService<PostgreSQLDatabaseInitializer>();
		var seeded = await initializer.SeedAsync(sqlScriptFile);
		if (!seeded)
			throw new InvalidOperationException("Failed to seed PostgreSQL database for feature flags");
		return services;
	}
}
