using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record ToggleFlagRequest(EvaluationMode EvaluationMode, string Reason);

public sealed class ToggleFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/toggle",
			async (
				string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				ToggleFlagRequest request,
				ToggleFlagHandler handler,
				CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
			})
		.AddEndpointFilter<ValidationFilter<ToggleFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("ToggleFlag")
		.WithTags("Feature Flags", "Operations", "Toggle Control", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();
	}
}

public sealed class ToggleFlagHandler(
		IDashboardRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<ToggleFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(string key,
		FlagRequestHeaders headers,
		ToggleFlagRequest request,
		CancellationToken cancellationToken)
	{
		var evaluationMode = request.EvaluationMode;
		try
		{
			var (isValid, result, source) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			// Check if the flag is already in the requested state
			if (source!.Configuration.ActiveEvaluationModes.ContainsModes([evaluationMode]))
			{
				logger.LogInformation("Feature flag {Key} is already {Status} - no change needed", key, evaluationMode);
				return Results.Ok(new FeatureFlagResponse(source));
			}

			// Store previous state for logging
			var previousModes = source.Configuration.ActiveEvaluationModes.Modes;

			// Reset scheduling, time window, and user/tenant access when manually toggling
			var config = new FlagEvaluationConfiguration(
				identifier: source.Identifier,
				activeEvaluationModes: new EvaluationModes([evaluationMode]),
				userAccessControl: new AccessControl(rolloutPercentage: evaluationMode == EvaluationMode.On ? 100 : 0),
				tenantAccessControl: new AccessControl(rolloutPercentage: evaluationMode == EvaluationMode.On ? 100 : 0));

			var flagWithUpdatedModes = new FeatureFlag(Identifier: source.Identifier, Metadata: source.Metadata, Configuration: config);
			flagWithUpdatedModes!.UpdateAuditTrail(action: evaluationMode == EvaluationMode.On ? "flag-enabled": "flag-disabled", 
				username: currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedModes, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

			var action = Enum.GetName(evaluationMode);
			logger.LogInformation("Feature flag {Key} {Action} by {User} (changed from {PreviousStatus}). Reason: {Reason}",
				key, action, currentUserService.UserName, JsonSerializer.Serialize(previousModes), request.Reason);

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

public sealed class ToggleFlagRequestValidator : AbstractValidator<ToggleFlagRequest>
{
	public ToggleFlagRequestValidator()
	{
		RuleFor(x => x.EvaluationMode)
			.IsInEnum()
			.WithMessage("EvaluationMode must be a valid value (Enabled or Disabled)");

		RuleFor(x => x.Reason)
			.NotEmpty()
			.WithMessage("Reason for toggling the flag is required")
			.MaximumLength(500)
			.WithMessage("Reason cannot exceed 500 characters");
	}
}