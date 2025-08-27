using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Propel.FeatureFlags.PostgresSql;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPostgresSqlFeatureFlags(this IServiceCollection services, string connectionString)
	{
		services.AddSingleton<IFeatureFlagRepository>(sp =>
				new PostgreSQLFeatureFlagRepository(
					connectionString ?? throw new InvalidOperationException("PostgreSQL connection string required"),
					sp.GetRequiredService<ILogger<PostgreSQLFeatureFlagRepository>>()));

		return services;
	}
}
