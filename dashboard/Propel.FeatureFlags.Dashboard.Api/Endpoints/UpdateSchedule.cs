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

public record UpdateScheduleRequest(DateTimeOffset? EnableOn, DateTimeOffset? DisableOn);

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
		.WithTags("Feature Flags", "Lifecycle Management", "Operations", "Management Api")
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
			var (isValid, result, source) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			bool isScheduleRemoval = request.EnableOn == null && request.DisableOn == null;

			var flagWithUpdatedSchedule = CreateFlagWithUpdatedSchedule(request, source!);
			flagWithUpdatedSchedule!.UpdateAuditTrail(action: isScheduleRemoval ? "schedule-removed" : "schedule-changed", 
				username: currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedSchedule, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

			var scheduleInfo = isScheduleRemoval
				? "removed schedule"
				: $"enable at {updatedFlag.Configuration.Schedule.EnableOn:yyyy-MM-dd HH:mm} UTC, disable at {updatedFlag.Configuration.Schedule.DisableOn:yyyy-MM-dd HH:mm} UTC";
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

	private FeatureFlag CreateFlagWithUpdatedSchedule(UpdateScheduleRequest request, FeatureFlag source)
	{
		var srcConfig = source!.Configuration;
		bool isScheduleRemoval = request.EnableOn == null && request.DisableOn == null;
		FlagEvaluationConfiguration configuration;
		if (isScheduleRemoval)
		{
			var modes = new EvaluationModes([.. source.Configuration.ActiveEvaluationModes.Modes]);
			modes.RemoveMode(EvaluationMode.Scheduled);

			configuration = new FlagEvaluationConfiguration(
				identifier: source.Identifier,
				activeEvaluationModes: modes,
				schedule: UtcSchedule.Unscheduled,
				operationalWindow: srcConfig.OperationalWindow,
				userAccessControl: srcConfig.UserAccessControl,
				tenantAccessControl: srcConfig.TenantAccessControl,
				targetingRules: srcConfig.TargetingRules,
				variations: srcConfig.Variations);
		}
		else
		{
			var modes = new EvaluationModes([.. source.Configuration.ActiveEvaluationModes.Modes]);
			modes.AddMode(EvaluationMode.Scheduled);
			var schedule = UtcSchedule.CreateSchedule(
				request.EnableOn ?? UtcDateTime.MinValue, 
				request.DisableOn ?? UtcDateTime.MaxValue);
			configuration = new FlagEvaluationConfiguration(
				identifier: source.Identifier,
				activeEvaluationModes: modes,
				schedule: schedule,
				operationalWindow: srcConfig.OperationalWindow,
				userAccessControl: srcConfig.UserAccessControl,
				tenantAccessControl: srcConfig.TenantAccessControl,
				targetingRules: srcConfig.TargetingRules,
				variations: srcConfig.Variations);
		}

		return new FeatureFlag(
					Identifier: source.Identifier,
					Metadata: source.Metadata,
					Configuration: configuration);
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