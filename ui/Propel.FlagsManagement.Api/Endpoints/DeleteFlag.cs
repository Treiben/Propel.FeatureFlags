using FeatureRabbit.Flags.Cache;
using FeatureRabbit.Flags.Persistence;
using FeatureRabbit.Management.Api.Endpoints.Shared;
using FluentValidation;

namespace FeatureRabbit.Management.Api.Endpoints;

public sealed class DeleteFlag : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapDelete("/api/feature-flags/{key}",
			async (string key,
					DeleteFlagHandler deleteFlagHandler) =>
		{
			return await deleteFlagHandler.HandleAsync(key);
		})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DeleteFeatureFlag")
		.WithTags("Feature Flags", "Management");
	}
}

public sealed class DeleteFlagHandler(CurrentUserService userService,
					IFeatureFlagRepository repository,
					IFeatureFlagCache cache,
					ILogger<DeleteFlag> logger)
{
	public async Task<IResult> HandleAsync(string key)
	{
		try
		{
			var existingFlag = await repository.GetAsync(key);
			if (existingFlag == null)
				return Results.NotFound($"Feature flag '{key}' not found");
			if (existingFlag.IsPermanent)
				return Results.BadRequest("Cannot delete permanent feature flag");

			await repository.DeleteAsync(key);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} deleted by {User}", key, userService.UserName);
			return Results.NoContent();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error deleting feature flag {Key}", key);
			return Results.StatusCode(500);
		}
	}
}
