using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public sealed class DeleteFlagEndpoint : IEndpoint
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
		.WithTags("Feature Flags", "CRUD Operations", "Delete", "Management Api")
		.Produces(StatusCodes.Status204NoContent);
	}
}

public sealed class DeleteFlagHandler(
	IFeatureFlagRepository repository,
	ICurrentUserService userService,
	ILogger<DeleteFlagHandler> logger,
	IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var existingFlag = await repository.GetAsync(key);
			if (existingFlag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			if (existingFlag.IsPermanent)
			{
				return HttpProblemFactory.BadRequest(
					"Cannot Delete Permanent Flag", 
					$"The feature flag '{key}' is marked as permanent and cannot be deleted. Remove the permanent flag first if deletion is required.", 
					logger);
			}

			var deleteResult = await repository.DeleteAsync(key);
			if (!deleteResult)
			{
				return HttpProblemFactory.InternalServerError(
					detail: "Failed to delete the feature flag from the repository", 
					logger: logger);
			}

			if (cache != null) await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} deleted successfully by {User}", 
				key, userService.UserName);
			
			return Results.NoContent();
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}
