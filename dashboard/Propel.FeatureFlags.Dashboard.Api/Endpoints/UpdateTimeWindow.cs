using FluentValidation;
using Knara.UtcStrict;
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
	bool RemoveTimeWindow,
	string? Notes);

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
		.WithTags("Feature Flags", "Lifecycle Management", "Operations", "Dashboard Api")
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
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			var flagWithUpdatedWindow = CreateFlagWithUpdatedTimeWindow(request, flag!);

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

	private FeatureFlag CreateFlagWithUpdatedTimeWindow(UpdateTimeWindowRequest request, FeatureFlag flag)
	{
		var oldconfig = flag.EvaluationOptions;

		// Remove enabled/disabled modes as we're configuring specific time window
		HashSet<EvaluationMode> modes = [.. oldconfig.ModeSet.Modes];
		modes.Remove(EvaluationMode.On);
		modes.Remove(EvaluationMode.Off);


		FlagEvaluationOptions configuration;
		FlagAdministration metadata;
		if (request.RemoveTimeWindow)
		{
			// If removing the time window, just remove the mode and clear the window
			modes.Remove(EvaluationMode.TimeWindow);
			configuration = oldconfig with {
				ModeSet = modes.Count == 0 ? EvaluationMode.Off : new ModeSet(modes),
				OperationalWindow = UtcTimeWindow.AlwaysOpen 
			};
			// Add to change history
			metadata = flag.Administration with
			{
				ChangeHistory = [.. flag.Administration.ChangeHistory,
					AuditTrail.FlagModified(currentUserService.UserName!, 
									request.Notes ?? (request.DaysActive.Count > 0 ? "Time window removed, days active updated" : "Time window removed"))]
			};
		}
		else
		{
			// Ensure TimeWindow mode is active
			modes.Add(EvaluationMode.TimeWindow);
			var window = new UtcTimeWindow(
					request.StartOn.ToTimeSpan(),
					request.EndOn.ToTimeSpan(),
					request.TimeZone,
					[.. request.DaysActive]);
			configuration = oldconfig with {
				ModeSet = modes.Count == 0 ? EvaluationMode.Off : new ModeSet(modes),
				OperationalWindow = window 
			};
			// Add to change history
			metadata = flag.Administration with
			{
				ChangeHistory = [.. flag.Administration.ChangeHistory,
					AuditTrail.FlagModified(currentUserService.UserName!, 
									notes: request.Notes ??  $"Time window updated: StartOn={request.StartOn:HH:mm}, EndOn={request.EndOn:HH:mm}, TimeZone={request.TimeZone}, DaysActive=[{string.Join(", ", request.DaysActive)}]" +
																(request.DaysActive.Count == 0 ? " (no days active - flag will never be active)" : ""))]
			};
		}

		return flag with { EvaluationOptions = configuration, Administration = metadata };
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


