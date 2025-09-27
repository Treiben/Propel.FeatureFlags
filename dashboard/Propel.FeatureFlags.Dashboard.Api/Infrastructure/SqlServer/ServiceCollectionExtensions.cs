using Microsoft.EntityFrameworkCore;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure.SqlServer;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddSqlServerDbContext(this IServiceCollection services, string connectionString)
	{
		services.AddDbContext<SqlServerDbContext>(options =>
		{
			options.UseSqlServer(connectionString, sqlOptions =>
			{
				sqlOptions.EnableRetryOnFailure(
					maxRetryCount: 3,
					maxRetryDelay: TimeSpan.FromSeconds(5),
					errorNumbersToAdd: null);
			});

			// Configure for development/production
			options.EnableSensitiveDataLogging(false);
			options.EnableDetailedErrors(false);
		});

		services.AddScoped<IDashboardRepository, DashboardRepository>();
		return services;
	}
}
