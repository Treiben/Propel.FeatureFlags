using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
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
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				ManageUserAccessRequest request,
				ManageUserAccessHandler handler,
				CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
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
		IFlagManagementRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<ManageUserAccessHandler> logger)
{
	public async Task<IResult> HandleAsync(
		string key,
		FlagRequestHeaders headers,
		ManageUserAccessRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);

			if (!isValid) return result;

			flag!.UpdateAuditTrail(currentUserService.UserName!);

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

			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Key, cancellationToken);

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


