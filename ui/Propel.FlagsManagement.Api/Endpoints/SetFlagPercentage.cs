using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record SetPercentageRequest(int Percentage);
public sealed class SetPercentageEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/percentage",
			async (
				string key,
				SetPercentageRequest request,
				SetPercentageHandler setPercentageHandler) =>
			{
				return await setPercentageHandler.HandleAsync(key, request);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<SetPercentageRequestValidator>>()
			.WithName("SetPercentage")
			.WithTags("Feature Flags", "Percentage Rollout", "Management")
			.Produces<FeatureFlagDto>();
	}

	public sealed class SetPercentageRequestValidator : AbstractValidator<SetPercentageRequest>
	{
		public SetPercentageRequestValidator()
		{
			RuleFor(c => c.Percentage)
				.Must(p => p >= 0 && p <= 100)
				.WithMessage("Feature flag rollout percentage must be between 0 and 100.");
		}
	}
}

public sealed class SetPercentageHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<SetPercentageHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, SetPercentageRequest request)
	{
		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
				return Results.NotFound($"Feature flag '{key}' not found");

			flag.Status = FeatureFlagStatus.Percentage;
			flag.PercentageEnabled = request.Percentage;
			flag.UpdatedBy = currentUserService.UserName;

			await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} percentage set to {Percentage}% by {User}",
				key, request.Percentage, currentUserService.UserName);

			return Results.Ok(new FeatureFlagDto(flag));
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error setting percentage for feature flag {Key}", key);
			return Results.StatusCode(500);
		}
	}
}


