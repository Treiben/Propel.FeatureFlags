using FluentValidation;
using Propel.FlagsManagement.Api.Endpoints;

namespace Propel.FlagsManagement.Api;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddValidators(this IServiceCollection services)
	{
		services.AddScoped<IValidator<CreateFlagRequest>, CreateFlagRequestValidator>();
		services.AddScoped<IValidator<UpdateFlagRequest>, UpdateFlagRequestValidator>();
		services.AddScoped<IValidator<EvaluateMultpleFlagsRequest>, EvaluateMultipleRequestValidator>();
		services.AddScoped<IValidator<UpdateScheduleRequest>, UpdateScheduleRequestValidator>();
		services.AddScoped<IValidator<UpdatePercentageRequest>, UpdatePercentageRequestValidator>();
		services.AddScoped<IValidator<ManageUsersRequest>, ManageUsersRequestValidator>();
		services.AddScoped<IValidator<EnableFlagRequest>, EnableFlagRequestValidator>();
		services.AddScoped<IValidator<DisableFlagRequest>, DisableFlagRequestValidator>();
		services.AddScoped<IValidator<GetFlagsRequest>, GetFlagsRequestValidator>();

		return services;
	}

	public static IServiceCollection AddHandlers(this IServiceCollection services)
	{
		services.AddScoped<ToggleFlagHandler>();
		services.AddScoped<UserAccessHandler>();
		services.AddScoped<UpdatePercentageHandler>();
		services.AddScoped<UpdateScheduleHandler>();
		services.AddScoped<ExpirationHandler>();
		services.AddScoped<SearchHandler>();
		services.AddScoped<CreateFlagHandler>();
		services.AddScoped<UpdateFlagHandler>();
		services.AddScoped<DeleteFlagHandler>();
		services.AddScoped<EvaluationHandler>();
		services.AddScoped<MultiFlagEvaluatorHandler>();

		return services;
	}
}
