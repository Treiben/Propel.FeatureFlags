using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Persistence;

namespace Propel.FeatureFlags.SqlServer;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddSqlServerFeatureFlags(this IServiceCollection services, FlagOptions options)
	{
		services.AddSingleton<IFeatureFlagRepository>(sp =>
			new SqlServerFeatureFlagRepository(
				options.SqlConnectionString ?? throw new InvalidOperationException("SqlServer connection string required"),
				sp.GetRequiredService<ILogger<SqlServerFeatureFlagRepository>>()));

		return services;
	}
}
