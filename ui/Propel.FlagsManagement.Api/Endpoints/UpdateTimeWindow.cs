using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record UpdateTimeWindowRequest(
	TimeOnly StartOn,
	TimeOnly EndOn, 
	string TimeZone, 
	List<DayOfWeek> DaysActive,
	bool RemoveTimeWindow);

public sealed class UpdateTimeWindowEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/time-window",
			async (
				string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				UpdateTimeWindowRequest request,
				UpdateTimeWindowHandler handler,
				CancellationToken cancellationToken) =>
		{
			return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
		})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.AddEndpointFilter<ValidationFilter<UpdateTimeWindowRequest>>()
		.WithName("SetTimeWindow")
		.WithTags("Feature Flags", "Lifecycle Management", "Operations", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();
	}
}

public sealed class UpdateTimeWindowHandler(
		IFlagManagementRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<UpdateTimeWindowHandler> logger)
{
	public async Task<IResult> HandleAsync(
		string key,
		FlagRequestHeaders headers,
		UpdateTimeWindowRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);

			if (!isValid) return result;

			// Update flag for time window
			flag!.UpdateAuditTrail(currentUserService.UserName!);
			flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.Enabled);

			if (request.RemoveTimeWindow)
			{
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.TimeWindow);
				flag.OperationalWindow = OperationalWindow.AlwaysOpen;
			}
			else
			{
				flag.ActiveEvaluationModes.AddMode(EvaluationMode.TimeWindow);

				flag.OperationalWindow = new OperationalWindow(
					request.StartOn.ToTimeSpan(),
					request.EndOn.ToTimeSpan(),
					request.TimeZone,
					[.. request.DaysActive]);
			}

			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Key, cancellationToken);

			logger.LogInformation("Feature flag {Key} time window updated by {User}",
				key, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class UpdateTimeWindowRequestValidator : AbstractValidator<UpdateTimeWindowRequest>
{
	public UpdateTimeWindowRequestValidator()
	{
		// Only validate when RemoveTimeWindow is false
		RuleFor(c => c.StartOn)
			.NotEqual(TimeOnly.MinValue)
			.When(c => !c.RemoveTimeWindow || c.DaysActive.Count > 0)
			.WithMessage("Window start time is required when setting a time window");

		RuleFor(c => c.EndOn)
			.GreaterThan(c => c.StartOn)
			.When(c => !c.RemoveTimeWindow && c.StartOn != TimeOnly.MinValue)
			.WithMessage("Window end time must be after window start time");

		RuleFor(c => c.TimeZone)
			.Must(BeValidTimeZone)
			.When(c => !c.RemoveTimeWindow && c.StartOn != TimeOnly.MinValue)
			.WithMessage("Time zone is required when setting a time window and must be a time zone identifier");
	}

	private static bool BeValidTimeZone(string? timeZone)
	{
		if (string.IsNullOrEmpty(timeZone)) return true;

		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById(timeZone) != null;
		}
		catch
		{
			return false;
		}
	}
}


