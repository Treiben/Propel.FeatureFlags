using Microsoft.EntityFrameworkCore;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.Postgres;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPostgresDbContext(this IServiceCollection services, string connectionString)
	{
		services.AddDbContext<PostgresDbContext>(options =>
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