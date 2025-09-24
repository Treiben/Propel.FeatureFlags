using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

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
		IDashboardRepository repository,
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
			var (isValid, result, source) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			var flagWithUpdatedWindow = CreateFlagWithUpdatedTimeWindow(request, source!);
			flagWithUpdatedWindow!.UpdateAuditTrail(action: "timewindow-changed", username:currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedWindow, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

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

	private FeatureFlag CreateFlagWithUpdatedTimeWindow(UpdateTimeWindowRequest request, FeatureFlag source)
	{
		var modes = new EvaluationModes([.. source.Configuration.ActiveEvaluationModes.Modes]);
		modes.RemoveMode(EvaluationMode.On);

		OperationalWindow window = OperationalWindow.AlwaysOpen;
		if (request.RemoveTimeWindow)
		{
			modes.RemoveMode(EvaluationMode.TimeWindow);
		}
		else
		{
			modes.AddMode(EvaluationMode.TimeWindow);
			window = new OperationalWindow(
				request.StartOn.ToTimeSpan(),
				request.EndOn.ToTimeSpan(),
				request.TimeZone,
				[.. request.DaysActive]);
		}

		var configuration = new FlagEvaluationConfiguration(
									identifier: source.Identifier,
									activeEvaluationModes: modes,
									schedule: source.Configuration.Schedule,
									operationalWindow: window,
									userAccessControl: source.Configuration.UserAccessControl,
									tenantAccessControl: source.Configuration.TenantAccessControl,
									targetingRules: source.Configuration.TargetingRules,
									variations: source.Configuration.Variations);

		return new FeatureFlag(Identifier: source.Identifier, Metadata: source.Metadata, Configuration: configuration);
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


