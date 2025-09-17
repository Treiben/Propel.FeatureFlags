using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Infrastructure;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public sealed class DeleteFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapDelete("/api/feature-flags/{key}",
			async (string key,
					string? reason,
					[FromHeader(Name = "X-Scope")] string scope,
					[FromHeader(Name = "X-Application-Name")] string? applicationName,
					[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
					DeleteFlagHandler deleteFlagHandler,
					CancellationToken cancellationToken) =>
			{
				return await deleteFlagHandler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), reason, cancellationToken);
			})
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("DeleteFeatureFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Delete", "Management Api")
		.Produces(StatusCodes.Status204NoContent)
		.Produces(StatusCodes.Status400BadRequest)
		.Produces(StatusCodes.Status404NotFound);
	}
}

public sealed class DeleteFlagHandler(
	IFlagManagementRepository repository,
	IFlagResolverService flagResolver,
	ICacheInvalidationService cacheInvalidationService,
	ICurrentUserService currentUserService,
	ILogger<DeleteFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(string key, FlagRequestHeaders headers, string? reason, CancellationToken cancellationToken)
	{
		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			if (flag!.Retention.IsPermanent)
			{
				return HttpProblemFactory.BadRequest(
					"Cannot Delete Permanent Flag",
					$"The feature flag '{key}' is marked as permanent and cannot be deleted. Remove the permanent flag first if deletion is required.",
					logger);
			}

			var deleteResult = await repository.DeleteAsync(flag.Key, currentUserService.UserName, reason ?? "Not specified", cancellationToken);

			await cacheInvalidationService.InvalidateFlagAsync(flag.Key, cancellationToken);


			logger.LogInformation("Feature flag {Key} deleted successfully by {User} for key {Key}",
				key, currentUserService.UserName, flag.Key);

			return Results.NoContent();
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			return HttpProblemFactory.ClientClosedRequest(logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}