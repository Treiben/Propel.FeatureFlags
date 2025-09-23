using Microsoft.EntityFrameworkCore;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.PostgresConfig;

public static class DbContextExtensions
{
	public static IServiceCollection AddPostgresDbContext(this IServiceCollection services, string connectionString)
	{
		services.AddDbContext<DashboardDbContext, PostgresDbContext>(options =>
		{
			options.UseNpgsql(connectionString, npgsqlOptions =>
			{
				npgsqlOptions.EnableRetryOnFailure(
					maxRetryCount: 3,
					maxRetryDelay: TimeSpan.FromSeconds(5),
					errorCodesToAdd: null);
			});

			// Configure for development/production
			options.EnableSensitiveDataLogging(false);
			options.EnableDetailedErrors(false);
		});

		return services;
	}
}
