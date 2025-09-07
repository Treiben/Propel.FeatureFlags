using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ManageUsersRequest(List<string> UserIds);

public record UpdateUserRolloutPercentageRequest(int Percentage);

public sealed class UpdateUserAccessControlEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/users/enable",
			async (
				string key,
				ManageUsersRequest request,
				ManageUserAccessHandler userAccessHandler) =>
		{
			return await userAccessHandler.HandleAsync(key, request.UserIds, true);
		})
		.AddEndpointFilter<ValidationFilter<ManageUsersRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("EnableForUser")
		.WithTags("Feature Flags", "Operations", "User Targeting", "Toggle Control", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();

		epRoutBuilder.MapPost("/api/feature-flags/{key}/users/disable",
			async (
				string key,
				ManageUsersRequest request,
				ManageUserAccessHandler userAccessHandler) =>
				{
					return await userAccessHandler.HandleAsync(key, request.UserIds, false);
				})
		.AddEndpointFilter<ValidationFilter<ManageUsersRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DisableForUser")
		.WithTags("Feature Flags", "Operations", "User Targeting", "Toggle Control", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();

		epRoutBuilder.MapPost("/api/feature-flags/{key}/users/rollout",
			async (
				string key,
				UpdateUserRolloutPercentageRequest request,
				UpdateUserRolloutPercentageHandler setPercentageHandler) =>
			{
				return await setPercentageHandler.HandleAsync(key, request);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<UpdateUserRolloutPercentageRequest>>()
			.WithName("UpdateUserRolloutPercentage")
			.WithTags("Feature Flags", "Operations", "User Targeting", "Rollout Control", "Management Api")
			.Produces<FeatureFlagResponse>()
			.ProducesValidationProblem();
	}
}

public sealed class ManageUserAccessHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<ManageUserAccessHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, List<string> userIds, bool enable)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		if (userIds == null || userIds.Count == 0)
		{
			return HttpProblemFactory.BadRequest("At least one user ID must be provided", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			foreach (var userId in userIds)
			{
				flag.UserAccess = enable ? flag.UserAccess.WithAllowedUser(userId) : flag.UserAccess.WithBlockedUser(userId);
			}

			flag.AuditRecord = new FlagAuditRecord(flag.AuditRecord.CreatedAt, flag.AuditRecord.CreatedBy, DateTime.UtcNow, currentUserService.UserName!);
			var updatedFlag = await repository.UpdateAsync(flag);

			if (cache != null) await cache.RemoveAsync(key);

			// Log detailed changes
			var action = enable ? "enabled" : "disabled";

			logger.LogInformation("Feature flag {Key} user access updated by {User}: {Action} for [{Users}] users.",
				key, currentUserService.UserName, action, string.Join(", ", userIds));

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class UpdateUserRolloutPercentageHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<UpdateUserRolloutPercentageHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, UpdateUserRolloutPercentageRequest request)
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
			flag.UserAccess = flag.UserAccess.WithRolloutPercentage(request.Percentage);
			if (request.Percentage == 0) // Special case: 0% effectively disables the flag
			{
				flag.EvaluationModeSet.RemoveMode(FlagEvaluationMode.UserRolloutPercentage);
			}
			else // Standard percentage rollout
			{
				flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
			}


			var updatedFlag = await repository.UpdateAsync(flag);

			if (cache != null) await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} user rollout percentage set to {Percentage}% by {User})",
				key, request.Percentage, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class ManageUsersRequestValidator : AbstractValidator<ManageUsersRequest>
{
	public ManageUsersRequestValidator()
	{
		RuleFor(x => x.UserIds)
			.NotEmpty()
			.WithMessage("At least one user ID must be provided");

		RuleFor(x => x.UserIds)
			.Must(userIds => userIds.Count <= 100)
			.WithMessage("Cannot manage more than 100 users at once");

		RuleForEach(x => x.UserIds)
			.NotEmpty()
			.WithMessage("User ID cannot be empty")
			.MaximumLength(100)
			.WithMessage("User ID cannot exceed 100 characters");

		RuleFor(x => x.UserIds)
			.Must(userIds => userIds.Distinct().Count() == userIds.Count)
			.WithMessage("Duplicate user IDs are not allowed");
	}
}

public sealed class UpdateUserPercentageRequestValidator : AbstractValidator<UpdateUserRolloutPercentageRequest>
{
	public UpdateUserPercentageRequestValidator()
	{
		RuleFor(c => c.Percentage)
			.InclusiveBetween(0, 100)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");
	}
}


