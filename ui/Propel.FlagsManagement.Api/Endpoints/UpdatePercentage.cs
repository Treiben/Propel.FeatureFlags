using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record UpdatePercentageRequest(int Percentage);

public sealed class UpdatePercentageEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/percentage",
			async (
				string key,
				UpdatePercentageRequest request,
				UpdatePercentageHandler setPercentageHandler) =>
			{
				return await setPercentageHandler.HandleAsync(key, request);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<UpdatePercentageRequest>>()
			.WithName("SetPercentage")
			.WithTags("Feature Flags", "Operations", "Rollout Control", "Management Api")
			.Produces<FeatureFlagDto>()
			.ProducesValidationProblem();
	}
}

public sealed class UpdatePercentageHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<UpdatePercentageHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, UpdatePercentageRequest request)
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

			// Special case: 0% effectively disables the flag
			if (request.Percentage == 0)
			{
				flag.Status = flag.Status.Decrement(FeatureFlagStatus.Percentage);
				flag.PercentageEnabled = 0;
			}
			// Standard percentage rollout
			else
			{
				flag.Status = flag.Status.Increment(FeatureFlagStatus.Percentage);
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

public sealed class UpdatePercentageRequestValidator : AbstractValidator<UpdatePercentageRequest>
{
	public UpdatePercentageRequestValidator()
	{
		RuleFor(c => c.Percentage)
			.InclusiveBetween(0, 100)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");
	}
}


