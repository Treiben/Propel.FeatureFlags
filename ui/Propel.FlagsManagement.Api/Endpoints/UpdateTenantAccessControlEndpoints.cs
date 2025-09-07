using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ManageTenantsAccessRequest(List<string> TenantIds);

public record UpdateTenantRolloutPercentageRequest(int Percentage);

public sealed class UpdateTenantAccessControlEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/tenants/enable",
			async (
				string key,
				ManageTenantsAccessRequest request,
				ManageTenantAccessHandler tenantAccessHandler) =>
		{
			return await tenantAccessHandler.HandleAsync(key, request.TenantIds, true);
		})
		.AddEndpointFilter<ValidationFilter<ManageTenantsAccessRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("EnableForTenant")
		.WithTags("Feature Flags", "Operations", "Tenant Targeting", "Toggle Control", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();

		epRoutBuilder.MapPost("/api/feature-flags/{key}/tenants/disable",
			async (
				string key,
				ManageTenantsAccessRequest request,
				ManageTenantAccessHandler tenantAccessHandler) =>
				{
					return await tenantAccessHandler.HandleAsync(key, request.TenantIds, false);
				})
		.AddEndpointFilter<ValidationFilter<ManageTenantsAccessRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DisableForTenant")
		.WithTags("Feature Flags", "Operations", "Tenant Targeting", "Toggle Control", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();

		epRoutBuilder.MapPost("/api/feature-flags/{key}/tenants/rollout",
			async (
				string key,
				UpdateTenantRolloutPercentageRequest request,
				UpdateTenantRolloutPercentageHandler setPercentageHandler) =>
			{
				return await setPercentageHandler.HandleAsync(key, request);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<UpdateTenantRolloutPercentageRequest>>()
			.WithName("UpdateTenantRolloutPercentage")
			.WithTags("Feature Flags", "Operations", "Tenant Targeting", "Rollout Control", "Management Api")
			.Produces<FeatureFlagResponse>()
			.ProducesValidationProblem();
	}
}

public sealed class ManageTenantAccessHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<ManageTenantAccessHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, List<string> tenantsIds, bool enable)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		if (tenantsIds == null || tenantsIds.Count == 0)
		{
			return HttpProblemFactory.BadRequest("At least one tenant ID must be provided", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			foreach (var tenantId in tenantsIds)
			{
				flag.TenantAccess = enable ? flag.TenantAccess.WithAllowedTenant(tenantId) : flag.TenantAccess.WithBlockedTenant(tenantId);
			}

			flag.AuditRecord = new FlagAuditRecord(flag.AuditRecord.CreatedAt, flag.AuditRecord.CreatedBy, DateTime.UtcNow, currentUserService.UserName!);
			var updatedFlag = await repository.UpdateAsync(flag);

			if (cache != null) await cache.RemoveAsync(key);

			// Log detailed changes
			var action = enable ? "enabled" : "disabled";

			logger.LogInformation("Feature flag {Key} tenant access updated by {User}: {Action} for [{Tenants}] tenants.",
				key, currentUserService.UserName, action, string.Join(", ", tenantsIds));

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class UpdateTenantRolloutPercentageHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<UpdateTenantRolloutPercentageHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, UpdateTenantRolloutPercentageRequest request)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			flag.AuditRecord = new FlagAuditRecord(flag.AuditRecord.CreatedAt, flag.AuditRecord.CreatedBy, DateTime.UtcNow, currentUserService.UserName!);
			flag.TenantAccess = flag.TenantAccess.WithRolloutPercentage(request.Percentage);
			if (request.Percentage == 0) // Special case: 0% effectively disables the flag
			{
				flag.EvaluationModeSet.RemoveMode(FlagEvaluationMode.TenantRolloutPercentage);
			}
			else // Standard percentage rollout
			{
				flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);
			}


			var updatedFlag = await repository.UpdateAsync(flag);

			if (cache != null) await cache.RemoveAsync(key);

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

public sealed class ManageTenantsAccessRequestValidator : AbstractValidator<ManageTenantsAccessRequest>
{
	public ManageTenantsAccessRequestValidator()
	{
		RuleFor(x => x.TenantIds)
			.NotEmpty()
			.WithMessage("At least one tenant ID must be provided");

		RuleFor(x => x.TenantIds)
			.Must(tenantIds => tenantIds.Count <= 100)
			.WithMessage("Cannot manage more than 100 tenants at once");

		RuleForEach(x => x.TenantIds)
			.NotEmpty()
			.WithMessage("Tenant ID cannot be empty")
			.MaximumLength(100)
			.WithMessage("Tenant ID cannot exceed 100 characters");

		RuleFor(x => x.TenantIds)
			.Must(tenantIds => tenantIds.Distinct().Count() == tenantIds.Count)
			.WithMessage("Duplicate tenant IDs are not allowed");
	}
}

public sealed class UpdateTenantPercentageRequestValidator : AbstractValidator<UpdateTenantRolloutPercentageRequest>
{
	public UpdateTenantPercentageRequestValidator()
	{
		RuleFor(c => c.Percentage)
			.InclusiveBetween(0, 100)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");
	}
}


