using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record UpdateFlagRequest
{
	public string? Name { get; set; }
	public string? Description { get; set; }
	public Dictionary<string, string>? Tags { get; set; }
	public bool IsPermanent { get; set; }
	public DateTime? ExpirationDate { get; set; }
}

public sealed class UpdateFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPut("/api/feature-flags/{key}",
			async (string key,
					UpdateFlagRequest request,
					UpdateFlagHandler handler,
					CancellationToken cancellationToken) =>
		{
			return await handler.HandleAsync(key, request, cancellationToken);
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
		IFeatureFlagRepository repository,
		ICurrentUserService userService,
		ILogger<UpdateFlagHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, UpdateFlagRequest request, CancellationToken cancellationToken)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var existingFlag = await repository.GetAsync(key);
			if (existingFlag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			ModifyFlagFromRequest(request, existingFlag, userService.UserName!);

			var updatedFlag = await repository.UpdateAsync(existingFlag, cancellationToken);

			if (cache != null) await cache.RemoveAsync(key, cancellationToken);

			logger.LogInformation("Feature flag {Key} updated by {User}", key, userService.UserName);
			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
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

	public static void ModifyFlagFromRequest(UpdateFlagRequest source, FeatureFlag dest, string updatedBy)
	{
		// Update only non-null properties from the request
		if (source.Name != null)
			dest.Name = source.Name;

		if (source.Description != null)
			dest.Description = source.Description;

		dest.AuditRecord = new FlagAuditRecord(dest.AuditRecord.CreatedAt, dest.AuditRecord.CreatedBy, DateTime.UtcNow, updatedBy);

		if (source.Tags != null)
			dest.Tags = source.Tags;

		dest.Lifecycle = new FlagLifecycle(isPermanent: source.IsPermanent, expirationDate: source.ExpirationDate);
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
