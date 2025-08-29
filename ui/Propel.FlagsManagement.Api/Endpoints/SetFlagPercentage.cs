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
			.AddEndpointFilter<ValidationFilter<SetPercentageRequest>>()
			.WithName("SetPercentage")
			.WithTags("Feature Flags", "Operations", "Rollout Control", "Management Api")
			.Produces<FeatureFlagDto>()
			.ProducesValidationProblem();
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

			// Validate business rules
			if (flag.IsPermanent)
			{
				return HttpProblemFactory.BadRequest(
					"Cannot Change Percentage of Permanent Flag",
					$"The feature flag '{key}' is marked as permanent and cannot have its percentage changed",
					logger);
			}

			// Special case: 0% effectively disables the flag
			if (request.Percentage == 0)
			{
				flag.Status = FeatureFlagStatus.Disabled;
				flag.PercentageEnabled = 0;
			}
			// Special case: 100% enables the flag for everyone
			else if (request.Percentage == 100)
			{
				flag.Status = FeatureFlagStatus.Enabled;
				flag.PercentageEnabled = 100;
			}
			// Standard percentage rollout
			else
			{
				flag.Status = FeatureFlagStatus.Percentage;
				flag.PercentageEnabled = request.Percentage;
			}

			flag.UpdatedBy = currentUserService.UserName!;

			var updatedFlag = await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			logger.LogInformation("Feature flag {Key} percentage set to {Percentage}% by {User} (status: {Status})",
				key, request.Percentage, currentUserService.UserName, flag.Status);

			return Results.Ok(new FeatureFlagDto(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class SetPercentageRequestValidator : AbstractValidator<SetPercentageRequest>
{
	public SetPercentageRequestValidator()
	{
		RuleFor(c => c.Percentage)
			.InclusiveBetween(0, 100)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");
	}
}


