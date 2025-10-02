using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record ManageUserAccessRequest(string[]? Allowed, string[]? Blocked, int? RolloutPercentage, string? Notes);

public sealed class UpdateUserAccessControlEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/users",
			async (
				string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				ManageUserAccessRequest request,
				ManageUserAccessHandler handler,
				CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<ManageUserAccessRequest>>()
			.WithName("UpdateUserAccessControl")
			.WithTags("Feature Flags", "Operations", "User Targeting", "Rollout Percentage", "Access Control Management", "Dashboard Api")
			.Produces<FeatureFlagResponse>()
			.ProducesValidationProblem();
	}
}

public sealed class ManageUserAccessHandler(
		IDashboardRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<ManageUserAccessHandler> logger)
{
	public async Task<IResult> HandleAsync(
		string key,
		FlagRequestHeaders headers,
		ManageUserAccessRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			var flagWithUpdatedUsers = CreateFlagWithUpdatedUsersAccess(request, flag!);

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedUsers, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

			logger.LogInformation("Feature flag {Key} user rollout percentage set to {Percentage}% by {User})",
				key, request.RolloutPercentage, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}

	private FeatureFlag CreateFlagWithUpdatedUsersAccess(ManageUserAccessRequest request, FeatureFlag flag)
	{
		var oldconfig = flag.EvaluationOptions;

		// Remove enabled/disabled modes as we're configuring specific access control
		HashSet<EvaluationMode> modes = [.. oldconfig.ModeSet.Modes];
		modes.Remove(EvaluationMode.On);
		modes.Remove(EvaluationMode.Off);

		// Ensure correct evaluation modes are set based on the request
		if (request.RolloutPercentage == 100) // Special case: 100% does not require any evaluation
		{
			modes.Remove(EvaluationMode.UserRolloutPercentage);
		}
		else // Standard percentage rollout
		{
			modes.Add(EvaluationMode.UserRolloutPercentage);
		}

		if (request.Allowed?.Length > 0 || request.Blocked?.Length > 0)
		{
			modes.Add(EvaluationMode.UserTargeted);
		}
		else // If no users are specified, remove the UserTargeted mode
		{
			modes.Remove(EvaluationMode.UserTargeted);
		}

		var accessControl = new AccessControl(
						allowed: [.. request.Allowed ?? []],
						blocked: [.. request.Blocked ?? []],
						rolloutPercentage: request.RolloutPercentage ?? oldconfig.UserAccessControl.RolloutPercentage);

		var configuration = oldconfig with {
			ModeSet = modes.Count == 0 ? EvaluationMode.Off : new ModeSet(modes),
			UserAccessControl = accessControl 
		};
		var metadata = flag.Administration with
		{
			ChangeHistory = [.. flag.Administration.ChangeHistory,
				AuditTrail.FlagModified(currentUserService.UserName!, notes: request.Notes ??  $"Updated user access control: " +
					$"AllowedUsers=[{string.Join(", ", accessControl.Allowed)}], " +
					$"BlockedUsers=[{string.Join(", ", accessControl.Blocked)}], " +
					$"RolloutPercentage={accessControl.RolloutPercentage}%")]
		};

		return flag with { EvaluationOptions = configuration, Administration = metadata };
	}
}

public sealed class ManageUserAccessRequestValidator : AbstractValidator<ManageUserAccessRequest>
{
	public ManageUserAccessRequestValidator()
	{
		RuleFor(c => c.RolloutPercentage)
			.InclusiveBetween(0, 100)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");

		RuleFor(c => c.Allowed)
			.Must(list => list == null || list.Distinct().Count() == list.Length)
			.WithMessage("Duplicate user IDs are not allowed in AllowedUsers");

		RuleFor(c => c.Blocked)
			.Must(list => list == null || list.Distinct().Count() == list.Length)
			.WithMessage("Duplicate user IDs are not allowed in BlockedUsers");

		RuleFor(c => c)
			.Must(c => c.Blocked!.Any(b => c.Allowed!.Contains(b)) == false)
			.When(c => c.Blocked != null && c.Allowed != null)
			.WithMessage("Users cannot be in both allowed and blocked lists");
	}
}


