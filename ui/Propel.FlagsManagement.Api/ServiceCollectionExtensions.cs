using FluentValidation;
using Propel.FlagsManagement.Api.Endpoints;

namespace Propel.FlagsManagement.Api;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddValidators(this IServiceCollection services)
	{
		services.AddScoped<IValidator<CreateFlagRequest>, CreateFlagRequestValidator>();
		services.AddScoped<IValidator<DisableFlagRequest>, DisableFlagRequestValidator>();
		services.AddScoped<IValidator<EnableFlagRequest>, EnableFlagRequestValidator>();
		services.AddScoped<IValidator<EvaluateMultpleFlagsRequest>, EvaluateMultipleRequestValidator>();
		services.AddScoped<IValidator<GetFlagsRequest>, GetFlagsRequestValidator>();
		services.AddScoped<IValidator<ManageUsersRequest>, ManageUsersRequestValidator>();
		services.AddScoped<IValidator<UpdateFlagRequest>, UpdateFlagRequestValidator>();
		services.AddScoped<IValidator<UpdatePercentageRequest>, UpdatePercentageRequestValidator>();
		services.AddScoped<IValidator<UpdateScheduleRequest>, UpdateScheduleRequestValidator>();
		services.AddScoped<IValidator<UpdateTimeWindowRequest>, UpdateTimeWindowRequestValidator>();

		return services;
	}

	public static IServiceCollection AddHandlers(this IServiceCollection services)
	{
		services.AddScoped<CreateFlagHandler>();
		services.AddScoped<DeleteFlagHandler>();
		services.AddScoped<EvaluationHandler>();
		services.AddScoped<ExpirationHandler>();
		services.AddScoped<MultiFlagEvaluatorHandler>();
		services.AddScoped<SearchHandler>();
		services.AddScoped<ToggleFlagHandler>();
		services.AddScoped<UpdateFlagHandler>();
		services.AddScoped<UpdatePercentageHandler>();
		services.AddScoped<UpdateScheduleHandler>();
		services.AddScoped<UpdateTimeWindowHandler>();
		services.AddScoped<UserAccessHandler>();

		return services;
	}
}
