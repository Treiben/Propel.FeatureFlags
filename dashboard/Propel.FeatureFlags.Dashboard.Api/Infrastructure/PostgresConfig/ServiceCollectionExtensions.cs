using Microsoft.EntityFrameworkCore;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.PostgresConfig;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPostgresDbContext(this IServiceCollection services, string connectionString)
	{
		// Assuming you have a DbContext class named DashboardDbContext
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

		services.AddScoped<IDashboardRepository, DashboardRepository>();
		return services;
	}
}
