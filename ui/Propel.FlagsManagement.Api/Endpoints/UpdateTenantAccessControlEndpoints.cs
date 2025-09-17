using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

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
		IFlagManagementRepository repository,
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
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);

			if (!isValid) return result;

			flag!.UpdateAuditTrail(currentUserService.UserName!);

			flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.Enabled);

			if (request.Percentage == 0) // Special case: 0% effectively disables the flag
			{
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.TenantRolloutPercentage);
			}
			else // Standard percentage rollout
			{
				flag.ActiveEvaluationModes.AddMode(EvaluationMode.TenantRolloutPercentage);
			}

			if (request.AllowedTenants?.Length > 0 || request.BlockedTenants?.Length > 0)
			{
				flag.ActiveEvaluationModes.AddMode(EvaluationMode.TenantTargeted);
			}
			else
			{
				// If no tenants are specified, remove the TenantTargeted mode
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.TenantTargeted);
			}

			flag.TenantAccessControl = new AccessControl(
							allowed: [.. request.AllowedTenants ?? []],
							blocked: [.. request.BlockedTenants ?? []],
							rolloutPercentage: request.Percentage ?? flag.TenantAccessControl.RolloutPercentage);


			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Key, cancellationToken);

			logger.LogInformation("Feature flag {Key} tenant rollout percentage set to {Percentage}% by {User})",
				key, request.Percentage, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
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


