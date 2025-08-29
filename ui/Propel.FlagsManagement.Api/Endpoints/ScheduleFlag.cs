using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ScheduleFlagRequest(DateTime EnableDate, DateTime? DisableDate);
public sealed class ScheduleFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/schedule",
			async (
				string key,
				ScheduleFlagRequest request,
				ScheduleFlagHandler scheduleFlagHandler) =>
		{
			return await scheduleFlagHandler.HandleAsync(key, request);
		})
		.RequireAuthorization()
		.AddEndpointFilter<ValidationFilter<ScheduleFlagRequestValidator>>()
		.WithName("ScheduleFeatureFlag")
		.WithTags("Feature Flags", "Scheduling", "Management")
		.Produces<FeatureFlagDto>();
	}

	public sealed class ScheduleFlagRequestValidator : AbstractValidator<ScheduleFlagRequest>
	{
		public ScheduleFlagRequestValidator()
		{
			RuleFor(c => c.EnableDate.ToUniversalTime())
				.LessThanOrEqualTo(DateTime.UtcNow)
				.WithMessage("Unable to schedule feature flag for backward date. Use simple toggling instead");
		}
	}
}

public sealed class ScheduleFlagHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<ScheduleFlagHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, ScheduleFlagRequest request)
	{
		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
				return Results.NotFound($"Feature flag '{key}' not found");

			flag.Status = FeatureFlagStatus.Scheduled;
			flag.ScheduledEnableDate = request.EnableDate;
			flag.ScheduledDisableDate = request.DisableDate;
			flag.UpdatedBy = currentUserService.UserName;

			await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} scheduled by {User} for {EnableDate}",
				key, currentUserService.UserName, request.EnableDate);

			return Results.Ok(new FeatureFlagDto(flag));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error scheduling feature flag {Key}", key);
			return Results.StatusCode(500);
		}
	}
}


