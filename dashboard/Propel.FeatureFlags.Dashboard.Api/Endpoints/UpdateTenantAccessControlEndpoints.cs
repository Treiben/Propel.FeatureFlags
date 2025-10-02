using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record ManageTenantAccessRequest(string[]? Allowed, string[]? Blocked, int? RolloutPercentage, string? Notes);

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
			.WithTags("Feature Flags", "Operations", "Tenant Targeting", "Rollout Percentage", "Access Control Management", "Dashboard Api")
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
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			var flagWithUpdatedTenants = CreateFlagWithUpdatedTenantAccess(request, flag!);

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedTenants, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

			logger.LogInformation("Feature flag {Key} tenant rollout percentage set to {Percentage}% by {User})",
				key, request.RolloutPercentage, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}

	private FeatureFlag CreateFlagWithUpdatedTenantAccess(ManageTenantAccessRequest request, FeatureFlag flag)
	{
		var oldconfig = flag.EvaluationOptions;

		// Remove enabled/disabled modes as we're configuring specific access control
		HashSet<EvaluationMode> modes = [.. oldconfig.ModeSet.Modes];
		modes.Remove(EvaluationMode.On);
		modes.Remove(EvaluationMode.Off);

		// Ensure correct evaluation modes are set based on the request
		if (request.RolloutPercentage == 100) // Special case: 100% does not require any evaluation
		{
			modes.Remove(EvaluationMode.TenantRolloutPercentage);
		}
		
		else // Standard percentage rollout
		{
			modes.Add(EvaluationMode.TenantRolloutPercentage);
		}

		if (request.Allowed?.Length > 0 || request.Blocked?.Length > 0)
		{
			modes.Add(EvaluationMode.TenantTargeted);
		}
		else // If no tenants are specified, remove the TenantTargeted mode
		{			
			modes.Remove(EvaluationMode.TenantTargeted);
		}

		var accessControl = new AccessControl(
						allowed: [.. request.Allowed ?? []],
						blocked: [.. request.Blocked ?? []],
						rolloutPercentage: request.RolloutPercentage ?? oldconfig.TenantAccessControl.RolloutPercentage);

		var configuration = oldconfig with {
			ModeSet = modes.Count == 0 ? EvaluationMode.Off : new ModeSet(modes),
			TenantAccessControl = accessControl 
		};
		var metadata = flag.Administration with
		{
			ChangeHistory = [.. flag.Administration.ChangeHistory,
				AuditTrail.FlagModified(currentUserService.UserName!, notes: request.Notes ??  $"Updated tenant access control: " +
					$"AllowedTenants=[{string.Join(", ", accessControl.Allowed)}], " +
					$"BlockedTenants=[{string.Join(", ", accessControl.Blocked)}], " +
					$"RolloutPercentage={accessControl.RolloutPercentage}%")]
		};

		return flag with { EvaluationOptions = configuration, Administration = metadata };
	}
}

public sealed class ManageTenantAccessRequestValidator : AbstractValidator<ManageTenantAccessRequest>
{
	public ManageTenantAccessRequestValidator()
	{
		RuleFor(c => c.RolloutPercentage)
			.InclusiveBetween(0, 100)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");

		RuleFor(c => c.Allowed)
		.Must(list => list == null || list.Distinct().Count() == list.Length)
		.WithMessage("Duplicate tenant IDs are not allowed in AllowedTenants");

		RuleFor(c => c.Blocked)
			.Must(list => list == null || list.Distinct().Count() == list.Length)
			.WithMessage("Duplicate tenant IDs are not allowed in BlockedTenants");

		RuleFor(c => c)
			.Must(c => c.Blocked!.Any(b => c.Allowed!.Contains(b)) == false)
			.When(c => c.Blocked != null && c.Allowed != null)
			.WithMessage("Tenants cannot be in both allowed and blocked lists");
	}
}


