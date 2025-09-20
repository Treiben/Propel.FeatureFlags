using Microsoft.Extensions.DependencyInjection;

namespace Propel.FeatureFlags.Migrations.SqlServer;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddMigrationServices(this IServiceCollection services)
	{
		services.AddSingleton<IMigrationEngine, MigrationEngine>();
		services.AddSingleton<Migrator>();
		services.AddSingleton<IMigrationRepository, SqlServerMigrationRepository>();

		return services;
	}
}
