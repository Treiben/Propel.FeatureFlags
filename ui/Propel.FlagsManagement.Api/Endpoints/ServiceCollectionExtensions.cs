using FluentValidation;

namespace Propel.FlagsManagement.Api.Endpoints;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddValidators(this IServiceCollection services)
	{
		services.AddScoped<IValidator<CreateFeatureFlagRequest>, CreateFeatureFlagRequestValidator>();
		services.AddScoped<IValidator<ModifyFlagRequest>, ModifyFeatureFlagRequestValidator>();
		services.AddScoped<IValidator<EvaluateMultpleFlagsRequest>, EvaluateMultipleRequestValidator>();
		services.AddScoped<IValidator<ScheduleFlagRequest>, ScheduleFlagRequestValidator>();
		services.AddScoped<IValidator<SetPercentageRequest>, SetPercentageRequestValidator>();
		services.AddScoped<IValidator<ManageUsersRequest>, ManageUsersRequestValidator>();
		services.AddScoped<IValidator<EnableFlagRequest>, EnableFlagRequestValidator>();
		services.AddScoped<IValidator<DisableFlagRequest>, DisableFlagRequestValidator>();

		return services;
	}

	public static IServiceCollection AddHandlers(this IServiceCollection services)
	{
		services.AddScoped<EnableFlagHandler>();
		services.AddScoped<DisableFlagHandler>();
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
