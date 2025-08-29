using FluentValidation;

namespace Propel.FlagsManagement.Api.Endpoints;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddValidators(this IServiceCollection services)
	{
		services.AddScoped<IValidator<CreateFeatureFlagRequest>, CreateFlag.CreateFeatureFlagRequestValidator>();
		services.AddScoped<IValidator<ModifyFlagRequest>, ModifyFlag.ModifyFeatureFlagRequestValidator>();
		services.AddScoped<IValidator<EvaluateMultipleRequest>, EvaluateMultipleEndpoint.EvaluateMultipleRequestValidator>();
		services.AddScoped<IValidator<ScheduleFlagRequest>, ScheduleFlagEndpoint.ScheduleFlagRequestValidator>();
		services.AddScoped<IValidator<SetPercentageRequest>, SetPercentageEndpoint.SetPercentageRequestValidator>();

		return services;
	}

	public static IServiceCollection AddHandlers(this IServiceCollection services)
	{
		services.AddScoped<ToggleFlagHandler>();
		services.AddScoped<UserAccessHandler>();
		services.AddScoped<SetPercentageHandler>();
		services.AddScoped<ScheduleFlagHandler>();
		services.AddScoped<ExpirationHandler>();
		services.AddScoped<SearchHandler>();
		services.AddScoped<CreateFlagHandler>();
		services.AddScoped<ModifyFlagHandler>();
		services.AddScoped<DeleteFlagHandler>();
		services.AddScoped<EvaluationHandler>();
		services.AddScoped<MultiFlagEvaluatorHandler>();

		return services;
	}
}
