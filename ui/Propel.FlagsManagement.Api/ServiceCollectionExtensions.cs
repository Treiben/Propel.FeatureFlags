using FluentValidation;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FeatureFlags.Infrastructure.Redis;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection RegisterPropelManagementApServicesi(this IServiceCollection services)
	{
		services.AddScoped<ICurrentUserService, CurrentUserService>();
		services.AddScoped<IFlagResolverService, FlagResolverService>();
		services.AddScoped<ICacheInvalidationService, CacheInvalidationService>();

		services.AddValidators();
		services.AddHandlers();
		services.RegisterApplicationEndpoints();
		return services;
	}

	public static IServiceCollection RegisterPropelFeatureFlagServices(this IServiceCollection services, FeatureFlagConfigurationOptions options)
	{
		//var featureFlagOptions = builder.Configuration.GetSection("PropelFeatureFlags").Get<FeatureFlagConfigurationOptions>() ?? new();
		services.AddFeatureFlagServices(options);
		services.AddPostgresSqlFeatureFlags(options.SqlConnectionString!);
		services.AddRedisCache(options.RedisConnectionString!);

		return services;
	}

	public static IServiceCollection AddPropelHealthchecks(this IServiceCollection services,
		string sqlConnection, string redisConnection)
	{
		// Add health checks with proper fallback handling
		var healthChecksBuilder = services.AddHealthChecks();

		// Add liveness check (always available)
		healthChecksBuilder.AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: ["liveness"]);

		// Add PostgreSQL health check only if connection string is available
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

	public static IServiceCollection AddValidators(this IServiceCollection services)
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

	public static IServiceCollection AddHandlers(this IServiceCollection services)
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