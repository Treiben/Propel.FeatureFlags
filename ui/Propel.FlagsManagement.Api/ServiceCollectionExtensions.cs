using FluentValidation;
using Propel.FlagsManagement.Api.Endpoints;

namespace Propel.FlagsManagement.Api;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddValidators(this IServiceCollection services)
	{
		services.AddScoped<IValidator<CreateFeatureFlagRequest>, CreateFlagRequestValidator>();
		services.AddScoped<IValidator<DisableFlagRequest>, DisableFlagRequestValidator>();
		services.AddScoped<IValidator<EnableFlagRequest>, EnableFlagRequestValidator>();
		services.AddScoped<IValidator<EvaluateFeatureFlagsRequest>, EvaluateMultipleRequestValidator>();
		services.AddScoped<IValidator<GetFeatureFlagRequest>, GetFlagsRequestValidator>();
		services.AddScoped<IValidator<ManageTenantAccessRequest>, ManageTenantAccessRequestValidator>();
		services.AddScoped<IValidator<ManageUserAccessRequest>, ManageUserAccessRequestValidator>();
		services.AddScoped<IValidator<UpdateFlagRequest>, UpdateFlagRequestValidator>();
		services.AddScoped<IValidator<UpdateScheduleRequest>, UpdateScheduleRequestValidator>();
		services.AddScoped<IValidator<UpdateTimeWindowRequest>, UpdateTimeWindowRequestValidator>();
		services.AddScoped<IValidator<UpdateTargetingRulesRequest>, UpdateTargetingRulesRequestValidator>();
		services.AddScoped<IValidator< TargetingRuleDto>, TargetingRuleDtoValidator>();

		return services;
	}

	public static IServiceCollection AddHandlers(this IServiceCollection services)
	{
		services.AddScoped<CreateFlagHandler>();
		services.AddScoped<DeleteFlagHandler>();
		services.AddScoped<FlagEvaluationHandler>();
		services.AddScoped<ManageTenantAccessHandler>();
		services.AddScoped<ManageUserAccessHandler>();
		services.AddScoped<UpdateTargetingRulesHandler>();
		services.AddScoped<ToggleFlagHandler>();
		services.AddScoped<UpdateFlagHandler>();
		services.AddScoped<UpdateScheduleHandler>();
		services.AddScoped<UpdateTimeWindowHandler>();

		return services;
	}
}