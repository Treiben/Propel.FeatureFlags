using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record UpdateScheduleRequest(DateTime EnableDate, DateTime? DisableDate, bool RemoveSchedule);

public sealed class UpdateScheduleEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/schedule",
			async (
				string key,
				UpdateScheduleRequest request,
				UpdateScheduleHandler scheduleFlagHandler) =>
		{
			return await scheduleFlagHandler.HandleAsync(key, request);
		})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.AddEndpointFilter<ValidationFilter<UpdateScheduleRequest>>()
		.WithName("SetSchedule" +
		"")
		.WithTags("Feature Flags", "Lifecycle Management", "Operations", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();
	}
}

public sealed class UpdateScheduleHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<UpdateScheduleHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, UpdateScheduleRequest request)
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
			flag.AuditRecord = new FlagAuditRecord(flag.AuditRecord.CreatedAt, flag.AuditRecord.CreatedBy, DateTime.UtcNow, currentUserService.UserName);

			if (request.RemoveSchedule)
			{
				flag.Schedule = FlagActivationSchedule.Unscheduled;
				flag.EvaluationModeSet.RemoveMode(FlagEvaluationMode.Scheduled);
			}
			else
			{
				flag.Schedule = FlagActivationSchedule.CreateSchedule(request.EnableDate, request.DisableDate);
				flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
			}

			var updatedFlag = await repository.UpdateAsync(flag);

			if (cache != null) await cache.RemoveAsync(key);

			var scheduleInfo = request.DisableDate.HasValue
				? $"enable at {flag.Schedule.ScheduledEnableUtcDate:yyyy-MM-dd HH:mm} UTC, disable at {flag.Schedule.ScheduledDisableUtcDate:yyyy-MM-dd HH:mm} UTC"
				: $"enable at {flag.Schedule.ScheduledEnableUtcDate:yyyy-MM-dd HH:mm} UTC";

			logger.LogInformation("Feature flag {Key} scheduled by {User} to {ScheduleInfo}",
				key, currentUserService.UserName, scheduleInfo);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
	public UpdateScheduleRequestValidator()
	{
		// Only validate dates when RemoveSchedule is false
		RuleFor(c => c.EnableDate)
			.GreaterThan(DateTime.UtcNow)
			.When(c => !c.RemoveSchedule)
			.WithMessage("Enable date must be in the future. Use immediate operations for current changes");

		RuleFor(c => c.DisableDate)
			.GreaterThan(c => c.EnableDate)
			.When(c => c.DisableDate.HasValue && !c.RemoveSchedule)
			.WithMessage("Disable date must be after enable date");

		RuleFor(c => c.DisableDate)
			.GreaterThan(DateTime.UtcNow)
			.When(c => c.DisableDate.HasValue && !c.RemoveSchedule)
			.WithMessage("Disable date must be in the future");
	}
}


