using FluentValidation;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
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
				ManageTenantAccessRequest request,
				ManageTenantAccessHandler accessHandler,
				CancellationToken cancellationToken) =>
			{
				return await accessHandler.HandleAsync(key, request, cancellationToken);
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
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<ManageTenantAccessHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, ManageTenantAccessRequest request,
		CancellationToken cancellationToken)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key, cancellationToken);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			flag.LastModified = new FeatureFlags.Core.Audit(timestamp: DateTime.UtcNow, actor: currentUserService.UserName!);

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

			if (cache != null) await cache.RemoveAsync(key, cancellationToken);

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


