using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Helpers;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

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
		IDashboardRepository repository,
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
			var (isValid, result, source) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			var flagWithUpdatedMeta = CreateFlagWithUpdatedMetadata(request, source!);
			flagWithUpdatedMeta!.UpdateAuditTrail(action: "metadata-changed", username: currentUserService.UserName!);

			var updatedFlag = await repository.UpdateMetadataAsync(flagWithUpdatedMeta!, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

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

	public static FeatureFlag CreateFlagWithUpdatedMetadata(UpdateFlagRequest requestData, FeatureFlag sourceFlag)
	{
		// Update only non-null properties from the request
		var metadata = Metadata.Create(
			identifier: sourceFlag.Identifier,
			name: string.IsNullOrWhiteSpace(requestData.Name) ? sourceFlag.Metadata.Name : requestData.Name,
			description: string.IsNullOrWhiteSpace(requestData.Description) ? sourceFlag.Metadata.Description : requestData.Description);

		if (requestData.Tags != null)
			metadata.Tags = requestData.Tags;
		else
			metadata.Tags = sourceFlag.Metadata.Tags;

		metadata.Retention = new RetentionPolicy(
					isPermanent: requestData.IsPermanent,
					expirationDate: DateTimeHelpers.NormalizeToUtc(requestData.ExpirationDate, sourceFlag.Metadata.Retention.ExpirationDate));

		return new FeatureFlag(Identifier: sourceFlag.Identifier,
			Metadata: metadata,
			Configuration: sourceFlag.Configuration);
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