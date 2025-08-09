using FeatureRabbit.Flags.Cache;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Flags.Persistence;
using FeatureRabbit.Management.Api.Endpoints.Shared;

namespace FeatureRabbit.Management.Api.Endpoints;

public record EnableFlagRequest(string Reason);
public sealed class EnableFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/enable",
			async (
				string key,
				EnableFlagRequest request,
				ToggleFlagHandler toggleFlagHandler) =>
		{
			if (request == null || string.IsNullOrWhiteSpace(request.Reason))
			{
				return Results.BadRequest("Reason for enabling the flag is required.");
			}
			return await toggleFlagHandler.HandleAsync(key, FeatureFlagStatus.Enabled, request.Reason);
		})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("EnableFlag")
		.WithTags("Feature Flags", "Toggle", "Management")
		.Produces<FeatureFlagDto>();
	}
}

public record DisableFlagRequest(string Reason);
public sealed class DisableFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/disable",
			async (
				string key,
				DisableFlagRequest request,
				ToggleFlagHandler toggleFlagHandler) =>
		{
			if (request == null || string.IsNullOrWhiteSpace(request.Reason))
			{
				return Results.BadRequest("Reason for disabling the flag is required.");
			}
			return await toggleFlagHandler.HandleAsync(key, FeatureFlagStatus.Disabled, request.Reason);
		})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DisableFlag")
		.WithTags("Feature Flags", "Toggle", "Management")
		.Produces<FeatureFlagDto>();
	}
}

public sealed class ToggleFlagHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<ToggleFlagHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, FeatureFlagStatus status, string? reason = null)
	{
		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
				return Results.NotFound($"Feature flag '{key}' not found");

			flag.Status = status;
			flag.UpdatedBy = currentUserService.UserName;

			await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} {Action} by {User}. Reason: {Reason}",
				key, status.ToString().ToLower(), currentUserService.UserName, reason ?? "Not provided");

			return Results.Ok(new FeatureFlagDto(flag));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error toggling feature flag {Key}", key);
			return Results.StatusCode(500);
		}
	}
}


