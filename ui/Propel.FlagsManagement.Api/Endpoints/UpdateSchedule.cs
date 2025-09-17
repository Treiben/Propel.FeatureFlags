using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

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
		IFlagManagementRepository repository,
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

			flag!.UpdateAuditTrail(currentUserService.UserName!);

			flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.Enabled);

			bool isScheduleRemoval = request.EnableOn == null && request.DisableOn == null;

			if (isScheduleRemoval)
			{
				flag.Schedule = ActivationSchedule.Unscheduled;
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.Scheduled);
			}
			else
			{
				flag.ActiveEvaluationModes.AddMode(EvaluationMode.Scheduled);
				flag.Schedule = ActivationSchedule.CreateSchedule(
					DateTimeHelpers.NormalizeToUtc(request.EnableOn, DateTime.MinValue.ToUniversalTime()),
					DateTimeHelpers.NormalizeToUtc(request.DisableOn, DateTime.MaxValue.ToUniversalTime())
					);
			}

			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Key, cancellationToken);

			var scheduleInfo = isScheduleRemoval
				? "removed schedule"
				: $"enable at {flag.Schedule.EnableOn:yyyy-MM-dd HH:mm} UTC, disable at {flag.Schedule.DisableOn:yyyy-MM-dd HH:mm} UTC";

			logger.LogInformation("Feature flag {Key} schedule updated by {User}: {ScheduleInfo}",
				key, currentUserService.UserName, scheduleInfo);

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