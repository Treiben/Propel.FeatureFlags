using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record UpdateFlagRequest(string? Name, string? Description, Dictionary<string, string>? Tags, bool IsPermanent, DateTimeOffset? ExpirationDate);

public sealed class UpdateFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPut("/api/feature-flags/{key}",
			async (string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				UpdateFlagRequest request,
				UpdateFlagHandler handler,
				CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
			})
		.AddEndpointFilter<ValidationFilter<UpdateFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("UpdateFeatureFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Update", "Management Api")
		.Produces<FeatureFlagResponse>()
		.ProducesValidationProblem();
	}
}

public sealed class UpdateFlagHandler(
		IFlagManagementRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<UpdateFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(string key,
		FlagRequestHeaders headers,
		UpdateFlagRequest request,
		CancellationToken cancellationToken)
	{

		try
		{	
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			ModifyFlagFromRequest(request, flag!);
			flag!.UpdateAuditTrail(currentUserService.UserName!);

			var updatedFlag = await repository.UpdateAsync(flag!, cancellationToken);

			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Key, cancellationToken);

			logger.LogInformation("Feature flag {Key} updated by {User}", key, currentUserService.UserName);
			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest("Invalid argument", ex.Message, logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}

	public static void ModifyFlagFromRequest(UpdateFlagRequest source, FeatureFlag dest)
	{
		// Update only non-null properties from the request
		if (source.Name != null)
			dest.Name = source.Name;

		if (source.Description != null)
			dest.Description = source.Description;

		if (source.Tags != null)
			dest.Tags = source.Tags;

		dest.Retention = new RetentionPolicy(
				isPermanent: source.IsPermanent,
				expirationDate: DateTimeHelpers.NormalizeToUtc(source.ExpirationDate, dest.Retention.ExpirationDate));
	}
}

public sealed class UpdateFlagRequestValidator : AbstractValidator<UpdateFlagRequest>
{
	public UpdateFlagRequestValidator()
	{
		RuleFor(c => c.Name)
			.MaximumLength(200)
			.When(c => !string.IsNullOrEmpty(c.Name))
			.WithMessage("Feature flag name must be between 1 and 200 characters");

		RuleFor(c => c.Description)
			.MaximumLength(1000)
			.When(c => !string.IsNullOrEmpty(c.Description))
			.WithMessage("Feature flag description cannot exceed 1000 characters");

		RuleFor(c => c.ExpirationDate)
			.GreaterThan(DateTime.UtcNow)
			.When(c => c.ExpirationDate.HasValue)
			.WithMessage("Expiration date must be in the future");
	}
}