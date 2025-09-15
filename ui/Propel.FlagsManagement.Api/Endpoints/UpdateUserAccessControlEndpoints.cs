using FluentValidation;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record ManageUserAccessRequest(string[]? AllowedUsers, string[]? BlockedUsers, int? Percentage);

public sealed class UpdateUserAccessControlEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/users",
			async (
				string key,
				ManageUserAccessRequest request,
				ManageUserAccessHandler accessHandler,
				CancellationToken cancellationToken) =>
			{
				return await accessHandler.HandleAsync(key, request, cancellationToken);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<ManageUserAccessRequest>>()
			.WithName("UpdateUserAccessControl")
			.WithTags("Feature Flags", "Operations", "User Targeting", "Rollout Percentage", "Access Control Management", "Management Api")
			.Produces<FeatureFlagResponse>()
			.ProducesValidationProblem();
	}
}

public sealed class ManageUserAccessHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<ManageUserAccessHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, ManageUserAccessRequest request,
		CancellationToken cancellationToken)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key, cancellationToken);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			flag.LastModified = new FeatureFlags.Core.Audit(timestamp: DateTime.UtcNow, actor: currentUserService.UserName!);

			flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.Enabled);

			if (request.Percentage == 0) // Special case: 0% effectively disables the flag
			{
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.UserRolloutPercentage);
			}
			else // Standard percentage rollout
			{
				flag.ActiveEvaluationModes.AddMode(EvaluationMode.UserRolloutPercentage);
			}

			if (request.AllowedUsers?.Length > 0 || request.BlockedUsers?.Length > 0)
			{
				flag.ActiveEvaluationModes.AddMode(EvaluationMode.UserTargeted);
			}
			else
			{
				// If no users are specified, remove the UserTargeted mode
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.UserTargeted);
			}

			flag.UserAccessControl = new AccessControl(
								allowed: [.. request.AllowedUsers ?? []],
								blocked: [.. request.BlockedUsers ?? []],
								rolloutPercentage: request.Percentage ?? flag.UserAccessControl.RolloutPercentage);


			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			if (cache != null) await cache.RemoveAsync(key, cancellationToken);

			logger.LogInformation("Feature flag {Key} user rollout percentage set to {Percentage}% by {User})",
				key, request.Percentage, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class ManageUserAccessRequestValidator : AbstractValidator<ManageUserAccessRequest>
{
	public ManageUserAccessRequestValidator()
	{
		RuleFor(c => c.Percentage)
			.InclusiveBetween(0, 100)
			.WithMessage("Feature flag rollout percentage must be between 0 and 100");

		RuleFor(c => c.AllowedUsers)
			.Must(list => list == null || list.Distinct().Count() == list.Length)
			.WithMessage("Duplicate user IDs are not allowed in AllowedUsers");

		RuleFor(c => c.BlockedUsers)
			.Must(list => list == null || list.Distinct().Count() == list.Length)
			.WithMessage("Duplicate user IDs are not allowed in BlockedUsers");

		RuleFor(c => c)
			.Must(c => c.BlockedUsers!.Any(b => c.AllowedUsers!.Contains(b)) == false)
			.When(c => c.BlockedUsers != null && c.AllowedUsers != null)
			.WithMessage("Users cannot be in both allowed and blocked lists");
	}
}


