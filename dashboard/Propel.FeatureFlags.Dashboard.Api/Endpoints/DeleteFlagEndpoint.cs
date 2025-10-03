using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public sealed class DeleteFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapDelete("/api/feature-flags/{key}",
			async (string key,
					[FromHeader(Name = "X-Scope")] string scope,
					[FromHeader(Name = "X-Application-Name")] string? applicationName,
					[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
					DeleteFlagHandler deleteFlagHandler,
					CancellationToken cancellationToken) =>
			{
				return await deleteFlagHandler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), cancellationToken);
			})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DeleteFeatureFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Delete", "Dashboard Api")
		.Produces(StatusCodes.Status204NoContent)
		.Produces(StatusCodes.Status400BadRequest)
		.Produces(StatusCodes.Status404NotFound)
		.Produces(StatusCodes.Status500InternalServerError);
	}
}

public sealed class DeleteFlagHandler(
	IDashboardRepository repository,
	IFlagResolverService flagResolver,
	ICacheInvalidationService cacheInvalidationService,
	ICurrentUserService currentUserService,
	ILogger<DeleteFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(string key, FlagRequestHeaders headers, CancellationToken cancellationToken)
	{
		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			if (flag!.Administration.RetentionPolicy.IsPermanent)
			{
				return HttpProblemFactory.BadRequest(
					"Cannot Delete Permanent Flag",
					$"The feature flag '{key}' is marked as permanent and cannot be deleted. To delete this flag, you must first remove the permanent retention policy through the flag's settings.",
					logger);
			}

			var deleteResult = await repository.DeleteAsync(flag.Identifier,
				currentUserService.UserName, "Flag deleted from dashboard", cancellationToken);

			if (!deleteResult)
			{
				return HttpProblemFactory.InternalServerError(
					null,
					logger,
					$"Failed to delete feature flag '{key}'. The flag exists but could not be removed from the database. Please try again or contact support.");
			}

			await cacheInvalidationService.InvalidateFlagAsync(flag.Identifier, cancellationToken);

			logger.LogInformation("Feature flag {Key} deleted successfully by {User}",
				key, currentUserService.UserName);

			return Results.NoContent();
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			return HttpProblemFactory.ClientClosedRequest(logger);
		}
		catch (InvalidOperationException ex)
		{
			// Handle state-related errors
			return HttpProblemFactory.UnprocessableEntity(
				$"Cannot delete feature flag '{key}': {ex.Message}",
				logger);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected error while deleting feature flag {Key}", key);
			return HttpProblemFactory.InternalServerError(
				ex,
				logger,
				$"An unexpected error occurred while deleting the feature flag '{key}'. Please try again or contact support if the problem persists.");
		}
	}
}