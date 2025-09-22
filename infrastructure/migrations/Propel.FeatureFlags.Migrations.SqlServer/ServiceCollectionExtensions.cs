using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Propel.FeatureFlags.Migrations.SqlServer;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddMigrationServices(this IServiceCollection services, IConfiguration configuration)
	{
		var optionsSection = configuration.GetSection("SqlMigrationOptions");
		var options = optionsSection.Get<SqlMigrationOptions>();
		services.AddSingleton(options);
		services.AddSingleton<IMigrationEngine, MigrationEngine>();
		services.AddSingleton<IMigrationRepository, SqlServerMigrationRepository>();
		services.AddSingleton<Migrator>();

		return services;
	}
}
