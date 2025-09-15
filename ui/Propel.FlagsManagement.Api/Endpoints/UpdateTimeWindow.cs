using FluentValidation;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
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
				UpdateTimeWindowRequest request,
				UpdateTimeWindowHandler timeWindowFlagHandler,
				CancellationToken cancellationToken) =>
		{
			return await timeWindowFlagHandler.HandleAsync(key, request, cancellationToken);
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
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<UpdateTimeWindowHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, UpdateTimeWindowRequest request,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key, cancellationToken);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			// Update flag for time window
			flag.LastModified = new FeatureFlags.Core.Audit(timestamp: DateTime.UtcNow, actor: currentUserService.UserName!);

			flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.Enabled);

			if (request.RemoveTimeWindow)
			{
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.TimeWindow);
				flag.OperationalWindow = FeatureFlags.Domain.OperationalWindow.AlwaysOpen;
			}
			else
			{
				flag.ActiveEvaluationModes.AddMode(EvaluationMode.TimeWindow);

				flag.OperationalWindow = FeatureFlags.Domain.OperationalWindow.CreateWindow(
					request.StartOn.ToTimeSpan(),
					request.EndOn.ToTimeSpan(),
					request.TimeZone,
					request.DaysActive);
			}

			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			if (cache != null) await cache.RemoveAsync(key, cancellationToken);

			logger.LogInformation("Feature flag {Key} time window updated by {User}",
				key, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			return HttpProblemFactory.ClientClosedRequest(logger);
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


