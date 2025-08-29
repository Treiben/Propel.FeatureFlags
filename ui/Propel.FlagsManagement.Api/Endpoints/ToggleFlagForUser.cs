using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ManageUsersRequest(List<string> UserIds);
public sealed class EnableForUserEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/users/enable",
			async (
				string key,
				ManageUsersRequest request,
				UserAccessHandler userAccessHandler) =>
		{
			if (request == null || request.UserIds?.Count <= 0)
			{
				return Results.BadRequest("Reason for enabling the flag is required.");
			}
			return await userAccessHandler.HandleAsync(key, request.UserIds, true);
		})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("EnableForUser")
		.WithTags("Feature Flags", "User Management", "Targeting")
		.Produces<FeatureFlagDto>();
	}
}

public sealed class DisableForUserEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/users/disable",
			async (
				string key,
				ManageUsersRequest request,
				UserAccessHandler userAccessHandler) =>
		{
			if (request == null || request.UserIds?.Count <= 0)
			{
				return Results.BadRequest("Reason for enabling the flag is required.");
			}
			return await userAccessHandler.HandleAsync(key, request.UserIds, false);
		})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DisableForUser")
		.WithTags("Feature Flags", "User Management", "Targeting")
		.Produces<FeatureFlagDto>();
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
		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
				return Results.NotFound($"Feature flag '{key}' not found");

			foreach (var userId in userIds)
			{
				if (enable)
				{
					if (!flag.EnabledUsers.Contains(userId))
						flag.EnabledUsers.Add(userId);
					flag.DisabledUsers.Remove(userId);
				}
				else
				{
					if (!flag.DisabledUsers.Contains(userId))
						flag.DisabledUsers.Add(userId);
					flag.EnabledUsers.Remove(userId);
				}
			}

			flag.UpdatedBy = currentUserService.UserName;
			await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			var action = enable ? "enabled" : "disabled";
			logger.LogInformation("Feature flag {Key} {Action} for users {Users} by {User}",
				key, action, string.Join(", ", userIds), currentUserService.UserName);

			return Results.Ok(new FeatureFlagDto(flag));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error managing user access for feature flag {Key}", key);
			return Results.StatusCode(500);
		}
	}
}


