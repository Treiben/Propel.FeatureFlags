using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record UpdateTimeWindowRequest(TimeOnly WindowStartTime,
	TimeOnly WindowEndTime, 
	string TimeZone, 
	List<DayOfWeek> WindowDays,
	bool RemoveTimeWindow);

public sealed class UpdateTimeWindowEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/time-window",
			async (
				string key,
				UpdateTimeWindowRequest request,
				UpdateTimeWindowHandler timeWindowFlagHandler) =>
		{
			return await timeWindowFlagHandler.HandleAsync(key, request);
		})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.AddEndpointFilter<ValidationFilter<UpdateTimeWindowRequest>>()
		.WithName("SetTimeWindow")
		.WithTags("Feature Flags", "Lifecycle Management", "Operations", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();
	}
}

public sealed class UpdateTimeWindowHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<UpdateTimeWindowHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, UpdateTimeWindowRequest request)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			// Update flag for scheduling
			flag.UpdatedBy = currentUserService.UserName!;

			if (request.RemoveTimeWindow)
			{
				flag.Status = flag.Status.Decrement(FeatureFlagStatus.TimeWindow);
				flag.WindowStartTime = null;
				flag.WindowEndTime = null;
				flag.WindowDays = null;
			}
			else
			{
				flag.Status = flag.Status.Increment(FeatureFlagStatus.TimeWindow);
				flag.WindowStartTime = request.WindowStartTime.ToTimeSpan();
				flag.WindowEndTime = request.WindowEndTime.ToTimeSpan();
				flag.TimeZone = request.TimeZone;
				if (request.WindowDays?.Count == 0)
				{
					// If no days are specified, assume all days
					flag.WindowDays = [.. Enum.GetValues<DayOfWeek>()];
				}
				else
					flag.WindowDays = request.WindowDays;
			}

			var updatedFlag = await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} time window updated by {User}",
				key, currentUserService.UserName);

			return Results.Ok(new FeatureFlagDto(updatedFlag));
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
		RuleFor(c => c.WindowStartTime)
			.NotEqual(TimeOnly.MinValue)
			.When(c => !c.RemoveTimeWindow || c.WindowDays.Count > 0)
			.WithMessage("Window start time is required when setting a time window");

		RuleFor(c => c.WindowEndTime)
			.GreaterThan(c => c.WindowStartTime)
			.When(c => !c.RemoveTimeWindow && c.WindowStartTime != TimeOnly.MinValue)
			.WithMessage("Window end time must be after window start time");

		RuleFor(c => c.TimeZone)
			.Must(BeValidTimeZone)
			.When(c => !c.RemoveTimeWindow && c.WindowStartTime != TimeOnly.MinValue)
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


