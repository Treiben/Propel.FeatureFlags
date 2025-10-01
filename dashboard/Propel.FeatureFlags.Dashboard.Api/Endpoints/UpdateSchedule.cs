using FluentValidation;
using Knara.UtcStrict;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record UpdateScheduleRequest(DateTimeOffset? EnableOn, DateTimeOffset? DisableOn, string? Notes);

public sealed class UpdateScheduleEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/schedule",
			async (
				string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				UpdateScheduleRequest request,
				UpdateScheduleHandler handler,
				CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
			})
		.AddEndpointFilter<ValidationFilter<UpdateScheduleRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("SetSchedule")
		.WithTags("Feature Flags", "Lifecycle Management", "Operations", "Dashboard Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();
	}
}

public sealed class UpdateScheduleHandler(
		IDashboardRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<UpdateScheduleHandler> logger)
{
	public async Task<IResult> HandleAsync(
		string key,
		FlagRequestHeaders headers,
		UpdateScheduleRequest request,
		CancellationToken cancellationToken)
	{

		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			bool isScheduleRemoval = request.EnableOn == null && request.DisableOn == null;

			var flagWithUpdatedSchedule = CreateFlagWithUpdatedSchedule(request, flag!, currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedSchedule, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

			var scheduleInfo = isScheduleRemoval
				? "removed schedule"
				: $"enable at {updatedFlag.EvaluationOptions.Schedule.EnableOn:yyyy-MM-dd HH:mm} UTC, disable at {updatedFlag.EvaluationOptions.Schedule.DisableOn:yyyy-MM-dd HH:mm} UTC";
			logger.LogInformation("Feature flag {Key} schedule updated by {User}: {ScheduleInfo}",
				key, currentUserService.UserName, JsonSerializer.Serialize(scheduleInfo));

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest("Invalid argument", ex.Message, logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}

	private FeatureFlag CreateFlagWithUpdatedSchedule(UpdateScheduleRequest request, FeatureFlag flag, string username)
	{
		var oldconfig = flag!.EvaluationOptions;
		bool removeSchedule = request.EnableOn == null && request.DisableOn == null;

		// Remove enabled/disabled modes as we're configuring specific scheduling
		HashSet<EvaluationMode> modes = [.. oldconfig.ModeSet.Modes];
		modes.Remove(EvaluationMode.On);
		modes.Remove(EvaluationMode.Off);

		FlagEvaluationOptions configuration;
		FlagAdministration metadata;

		if (removeSchedule)
		{
			// Remove scheduled mode as we're removing scheduling
			modes.Remove(EvaluationMode.Scheduled);
			// Reset schedule to unscheduled
			configuration = oldconfig with
			{
				ModeSet = modes.Count == 0 ? EvaluationMode.Off : new ModeSet(modes),
				Schedule = UtcSchedule.Unscheduled
			};
			// Add to change history
			metadata = flag.Administration with
			{
				ChangeHistory =
				[
					.. flag.Administration.ChangeHistory,
					AuditTrail.FlagModified(username: username, notes: request.Notes ?? "Removed flag schedule"),
				]
			};
		}
		else
		{
			// Ensure the Scheduled mode is included
			modes.Add(EvaluationMode.Scheduled);
			// Update the schedule
			configuration = oldconfig with
			{
				ModeSet = new ModeSet(modes),
				Schedule = UtcSchedule.CreateSchedule(
					request.EnableOn ?? UtcDateTime.MinValue,
					request.DisableOn ?? UtcDateTime.MaxValue)
			};
			// Add to change history
			metadata = flag.Administration with
			{
				ChangeHistory =
				[
					.. flag.Administration.ChangeHistory,
					AuditTrail.FlagModified(username: username, notes: request.Notes ?? "Updated flag schedule"),
				]
			};
		}

		return flag with { EvaluationOptions = configuration, Administration = metadata };
	}
}

public sealed class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
	public UpdateScheduleRequestValidator()
	{
		RuleFor(x => x.EnableOn)
			.GreaterThan(DateTimeOffset.UtcNow)
			.When(x => x.EnableOn.HasValue)
			.WithMessage("Enable date must be in the future");

		RuleFor(x => x.DisableOn)
			.GreaterThan(DateTimeOffset.UtcNow)
			.When(x => x.DisableOn.HasValue)
			.WithMessage("Disable date must be in the future");

		RuleFor(x => x.DisableOn)
			.GreaterThan(x => x.EnableOn)
			.When(x => x.EnableOn.HasValue && x.DisableOn.HasValue)
			.WithMessage("Disable date must be after enable date");
	}
}