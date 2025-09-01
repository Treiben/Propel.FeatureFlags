using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ManageUsersRequest(List<string> UserIds);

public sealed class ToggleFlagForUserEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/users/enable",
			async (
				string key,
				ManageUsersRequest request,
				UserAccessHandler userAccessHandler) =>
		{
			return await userAccessHandler.HandleAsync(key, request.UserIds, true);
		})
		.AddEndpointFilter<ValidationFilter<ManageUsersRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("EnableForUser")
		.WithTags("Feature Flags", "Operations", "User Targeting", "Toggle Control", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();

		epRoutBuilder.MapPost("/api/feature-flags/{key}/users/disable",
			async (
				string key,
				ManageUsersRequest request,
				UserAccessHandler userAccessHandler) =>
				{
					return await userAccessHandler.HandleAsync(key, request.UserIds, false);
				})
		.AddEndpointFilter<ValidationFilter<ManageUsersRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DisableForUser")
		.WithTags("Feature Flags", "Operations", "User Targeting", "Toggle Control", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();
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

public sealed class UserAccessHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<UserAccessHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, List<string> userIds, bool enable)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		// Validate userIds parameter
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

			// Track changes for logging
			var addedUsers = new List<string>();
			var removedUsers = new List<string>();
			var noChangeUsers = new List<string>();

			foreach (var userId in userIds)
			{
				if (enable)
				{
					// Enable user: add to enabled list, remove from disabled list
					if (!flag.EnabledUsers.Contains(userId))
					{
						flag.EnabledUsers.Add(userId);
						addedUsers.Add(userId);
					}
					else
					{
						noChangeUsers.Add(userId);
					}

					if (flag.DisabledUsers.Remove(userId))
					{
						removedUsers.Add($"{userId} (from disabled)");
					}
				}
				else
				{
					// Disable user: add to disabled list, remove from enabled list
					if (!flag.DisabledUsers.Contains(userId))
					{
						flag.DisabledUsers.Add(userId);
						addedUsers.Add(userId);
					}
					else
					{
						noChangeUsers.Add(userId);
					}

					if (flag.EnabledUsers.Remove(userId))
					{
						removedUsers.Add($"{userId} (from enabled)");
					}
				}
			}

			flag.UpdatedBy = currentUserService.UserName!;
			var updatedFlag = await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			// Log detailed changes
			var action = enable ? "enabled" : "disabled";
			var targetList = enable ? "enabled users" : "disabled users";

			logger.LogInformation(
				"Feature flag {Key} user access updated by {User}: {Action} for {UserCount} users. " +
				"Added to {TargetList}: [{AddedUsers}]. Removed: [{RemovedUsers}]. No change: [{NoChangeUsers}]",
				key, currentUserService.UserName, action, userIds.Count, targetList,
				string.Join(", ", addedUsers), string.Join(", ", removedUsers), string.Join(", ", noChangeUsers));

			return Results.Ok(new FeatureFlagDto(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}


