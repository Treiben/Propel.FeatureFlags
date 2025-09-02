using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ResetFlagRequest(string Reason);
public sealed class ResetFlagEndpoints: IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/reset-schedule",
			async (
				string key,
				ResetFlagRequest request, 
				ResetHandler resetHandler) =>
			{
				return await resetHandler.HandleAsync(key, "schedule", request.Reason);
			})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("ResetSchedule")
		.WithTags("Feature Flags", "Operations", "Schedule Control", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();

		epRoutBuilder.MapPost("/api/feature-flags/reset-time-window",
			async (
				string key,
				ResetFlagRequest request,
				ResetHandler resetHandler) =>
			{
				return await resetHandler.HandleAsync(key, "timeWindow", request.Reason);
			})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("ResetTimeWindow")
		.WithTags("Feature Flags", "Operations", "Time Window Control", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();
	}
}

public sealed class ResetHandler(
						IFeatureFlagRepository repository,
						IFeatureFlagCache cache,
						ILogger<DisableFlagHandler> logger,
						CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, string resetType, string reason)
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

			// Update flag
			if (resetType == "schedule")
			{
				flag.ScheduledEnableDate = null;
				flag.ScheduledDisableDate = null;
			}

			if (resetType == "percentage")
			{
				flag.PercentageEnabled = 0;
			}

			if (resetType == "timeWindow")
			{
				flag.WindowStartTime = null;
				flag.WindowEndTime = null;
				flag.WindowDays = null;
				flag.TimeZone = null;
			}
			flag.UpdatedBy = currentUserService.UserName!;

			// Reset scheduling and percentage when manually toggling
			flag.ScheduledEnableDate = null;
			flag.ScheduledDisableDate = null;
			flag.PercentageEnabled = 0;

			var updatedFlag = await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} {ResetType} was reset back to defaults by {User}. Reason: {Reason}",
				key, resetType, currentUserService.UserName, reason);

			return Results.Ok(new FeatureFlagDto(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}
