using FluentValidation;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Extensions;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FeatureFlags.Infrastructure.Redis.Extensions;

namespace Propel.FeatureFlags.Dashboard.Api;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddDashboardServices(this IServiceCollection services)
	{
		services.AddScoped<ICurrentUserService, CurrentUserService>();
		services.AddScoped<IFlagResolverService, FlagResolverService>();
		services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

		services.AddValidators();
		services.AddHandlers();
		services.RegisterDashboardEndpoints();
		return services;
	}

	public static IServiceCollection AddFeatureFlagCoreServices(this IServiceCollection services, PropelOptions options)
	{
		services.AddPropelServices(options);
		services.AddPropelPersistence(options.Database.DefaultConnection!);
		services.AddPropelDistributedCache(options.Cache.Connection!);

		return services;
	}

	public static IServiceCollection AddDashboardHealthchecks(this IServiceCollection services, PropelOptions options)
	{
		// Add health checks with proper fallback handling
		var healthChecksBuilder = services.AddHealthChecks();

		// Add liveness check (always available)
		healthChecksBuilder.AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: ["liveness"]);

		// Add PostgreSQL health check only if connection string is available
		var sqlConnection = options.Database.DefaultConnection ?? string.Empty;
		if (!string.IsNullOrEmpty(sqlConnection))
		{
			healthChecksBuilder.AddNpgSql(
				connectionString: sqlConnection,
				healthQuery: "SELECT 1;",
				name: "postgres",
				failureStatus: HealthStatus.Unhealthy,
				tags: ["database", "postgres", "readiness"]);
		}

		// Add Redis health check only if connection string is available
		var redisConnection = options.Cache.Connection ?? string.Empty;
		if (!string.IsNullOrEmpty(redisConnection))
		{
			healthChecksBuilder.AddRedis(
				redisConnectionString: redisConnection,
				name: "redis",
				failureStatus: HealthStatus.Degraded,
				tags: ["cache", "redis", "readiness"]);
		}

		return services;
	}

	private static IServiceCollection AddValidators(this IServiceCollection services)
	{
		services.AddScoped<IValidator<CreateGlobalFeatureFlagRequest>, CreateFlagRequestValidator>();
		services.AddScoped<IValidator<GetFeatureFlagRequest>, GetFlagsRequestValidator>();
		services.AddScoped<IValidator<ManageTenantAccessRequest>, ManageTenantAccessRequestValidator>();
		services.AddScoped<IValidator<ManageUserAccessRequest>, ManageUserAccessRequestValidator>();
		services.AddScoped<IValidator<UpdateFlagRequest>, UpdateFlagRequestValidator>();
		services.AddScoped<IValidator<UpdateScheduleRequest>, UpdateScheduleRequestValidator>();
		services.AddScoped<IValidator<UpdateTimeWindowRequest>, UpdateTimeWindowRequestValidator>();
		services.AddScoped<IValidator<UpdateTargetingRulesRequest>, UpdateTargetingRulesRequestValidator>();
		services.AddScoped<IValidator< TargetingRuleRequest>, TargetingRuleDtoValidator>();

		return services;
	}

	private static IServiceCollection AddHandlers(this IServiceCollection services)
	{
		services.AddScoped<CreateGlobalFlagHandler>();
		services.AddScoped<DeleteFlagHandler>();
		services.AddScoped<FlagEvaluationHandler>();
		services.AddScoped<ManageTenantAccessHandler>();
		services.AddScoped<ManageUserAccessHandler>();
		services.AddScoped<ToggleFlagHandler>();
		services.AddScoped<UpdateFlagHandler>();
		services.AddScoped<UpdateScheduleHandler>();
		services.AddScoped<UpdateTargetingRulesHandler>();
		services.AddScoped<UpdateTimeWindowHandler>();

		return services;
	}
}