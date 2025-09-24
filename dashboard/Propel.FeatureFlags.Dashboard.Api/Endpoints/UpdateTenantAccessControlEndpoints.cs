using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record ManageTenantAccessRequest(string[]? AllowedTenants, string[]? BlockedTenants, int? Percentage);

public sealed class UpdateTenantAccessControlEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/tenants",
			async (
				string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				ManageTenantAccessRequest request,
				ManageTenantAccessHandler handler,
				CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<ManageTenantAccessRequest>>()
			.WithName("UpdateTenantAccessControl")
			.WithTags("Feature Flags", "Operations", "Tenant Targeting", "Rollout Percentage", "Access Control Management", "Management Api")
			.Produces<FeatureFlagResponse>()
			.ProducesValidationProblem();
	}
}

public sealed class ManageTenantAccessHandler(
		IDashboardRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<ManageTenantAccessHandler> logger)
{
	public async Task<IResult> HandleAsync(
		string key,
		FlagRequestHeaders headers,
		ManageTenantAccessRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			var (isValid, result, source) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			var flagWithUpdatedTenants = CreateFlagWithUpdatedTenantAccess(request, source);
			flagWithUpdatedTenants!.UpdateAuditTrail(action: "tenant-access-changed", username:currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedTenants, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

			logger.LogInformation("Feature flag {Key} tenant rollout percentage set to {Percentage}% by {User})",
				key, request.Percentage, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}

	private FeatureFlag CreateFlagWithUpdatedTenantAccess(ManageTenantAccessRequest request, FeatureFlag source)
	{
		var modes = new EvaluationModes([.. source.Configuration.ActiveEvaluationModes.Modes]);
		modes.RemoveMode(EvaluationMode.On);

		if (request.Percentage == 0) // Special case: 0% effectively disables the flag
		{
			modes.RemoveMode(EvaluationMode.TenantRolloutPercentage);
		}
		else // Standard percentage rollout
		{
			modes.AddMode(EvaluationMode.TenantRolloutPercentage);
		}

		if (request.AllowedTenants?.Length > 0 || request.BlockedTenants?.Length > 0)
		{
			modes.AddMode(EvaluationMode.TenantTargeted);
		}
		else
		{
			// If no tenants are specified, remove the TenantTargeted mode
			modes.RemoveMode(EvaluationMode.TenantTargeted);
		}

		var accessControl = new AccessControl(
						allowed: [.. request.AllowedTenants ?? []],
						blocked: [.. request.BlockedTenants ?? []],
						rolloutPercentage: request.Percentage ?? source.Configuration.TenantAccessControl.RolloutPercentage);

		var configuration = new FlagEvaluationConfiguration(
									identifier: source.Identifier,
									activeEvaluationModes: modes,
									schedule: source.Configuration.Schedule,
									operationalWindow: source.Configuration.OperationalWindow,
									userAccessControl: source.Configuration.UserAccessControl,
									tenantAccessControl: accessControl,
									targetingRules: source.Configuration.TargetingRules,
									variations: source.Configuration.Variations);
		return new FeatureFlag(source.Identifier, Metadata: source.Metadata, Configuration: configuration);
	}
}

public sealed class ManageTenantAccessRequestValidator : AbstractValidator<ManageTenantAccessRequest>
{
	public ManageTenantAccessRequestValidator()
	{
		RuleFor(c => c.Percentage)
			.InclusiveBetween(0, 100)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");

		RuleFor(c => c.AllowedTenants)
		.Must(list => list == null || list.Distinct().Count() == list.Length)
		.WithMessage("Duplicate tenant IDs are not allowed in AllowedTenants");

		RuleFor(c => c.BlockedTenants)
			.Must(list => list == null || list.Distinct().Count() == list.Length)
			.WithMessage("Duplicate tenant IDs are not allowed in BlockedTenants");

		RuleFor(c => c)
			.Must(c => c.BlockedTenants!.Any(b => c.AllowedTenants!.Contains(b)) == false)
			.When(c => c.BlockedTenants != null && c.AllowedTenants != null)
			.WithMessage("Tenants cannot be in both allowed and blocked lists");
	}
}


