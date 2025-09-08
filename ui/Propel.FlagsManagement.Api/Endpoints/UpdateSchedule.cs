using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;
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
				UpdateScheduleHandler scheduleFlagHandler, 
				CancellationToken cancellationToken) =>
		{
			return await scheduleFlagHandler.HandleAsync(key, request, cancellationToken);
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
	public async Task<IResult> HandleAsync(string key, UpdateScheduleRequest request, CancellationToken cancellationToken)
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

			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			if (cache != null) await cache.RemoveAsync(key, cancellationToken);

			var scheduleInfo = request.DisableDate.HasValue
				? $"enable at {flag.Schedule.ScheduledEnableDate:yyyy-MM-dd HH:mm} UTC, disable at {flag.Schedule.ScheduledDisableDate:yyyy-MM-dd HH:mm} UTC"
				: $"enable at {flag.Schedule.ScheduledEnableDate:yyyy-MM-dd HH:mm} UTC";

			logger.LogInformation("Feature flag {Key} scheduled by {User} to {ScheduleInfo}",
				key, currentUserService.UserName, scheduleInfo);

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


