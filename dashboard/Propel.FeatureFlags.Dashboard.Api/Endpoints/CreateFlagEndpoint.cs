using FluentValidation;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;
using System.Reflection.PortableExecutable;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

// Assertions:
// The api is allowed to create only global feature flags for now (no application specific flags).
// The global feature flags are created with a RetentionPolicy thas is permanent by default and has not expiration date.
public record CreateGlobalFeatureFlagRequest
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public Dictionary<string, string>? Tags { get; set; }
}

public sealed class CreateFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{
		app.MapPost("/api/feature-flags",
			async (CreateGlobalFeatureFlagRequest request,
					CreateGlobalFlagHandler createFlagHandler,
					CancellationToken cancellationToken) =>
		{
			return await createFlagHandler.HandleAsync(request, cancellationToken);
		})
		.AddEndpointFilter<ValidationFilter<CreateGlobalFeatureFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("CreateFeatureFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Create", "Dashboard Api")
		.Produces<FeatureFlagResponse>(StatusCodes.Status201Created);
	}
}

public sealed class CreateGlobalFlagHandler(
		IDashboardRepository repository,
		ICurrentUserService currentUserService,
		ILogger<CreateGlobalFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(CreateGlobalFeatureFlagRequest request, CancellationToken cancellationToken)
	{
		try
		{
			var identifier = new FlagIdentifier(request.Key, Scope.Global);

			var flagExists = await repository.FlagExistsAsync(identifier, cancellationToken);
			if (flagExists)
			{
				return HttpProblemFactory.Conflict(
					$"A feature flag with the key '{request.Key}' already exists. Please use a different key or update the existing flag.",
					logger);
			}

			var metadata = Metadata.Create(
				Scope.Global,
				request.Name,
				request.Description ?? string.Empty,
				AuditTrail.FlagCreated(currentUserService.UserName!)) with { Tags = request.Tags ?? [] };

			var globalFlag = new FeatureFlag(identifier, metadata, EvalConfiguration.DefaultConfiguration);

			logger.LogInformation("Feature flag {Key} created successfully by {User}",
				identifier.Key, currentUserService.UserName);

			var flag = await repository.CreateAsync(globalFlag, cancellationToken);

			return Results.Created($"/api/feature-flags/{globalFlag.Identifier.Key}", new FeatureFlagResponse(globalFlag));
		}

		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class CreateFlagRequestValidator : AbstractValidator<CreateGlobalFeatureFlagRequest>
{
	public CreateFlagRequestValidator()
	{
		RuleFor(c => c.Key)
			.NotEmpty()
			.WithMessage("Feature flag key must be provided");
		RuleFor(c => c.Key)
			.Matches(@"^[a-zA-Z0-9_-]+$")
			.WithMessage("Feature flag key can only contain letters, numbers, hyphens, and underscores.");
		RuleFor(c => c.Key)
			.Length(1, 100)
			.WithMessage("Feature flag key must be between 1 and 100 characters");

		RuleFor(c => c.Name)
			.NotEmpty()
			.WithMessage("Feature flag name must be provided");

		RuleFor(c => c.Name)
			.Length(1, 200)
			.WithMessage("Feature flag name must be between 1 and 200 characters");

		RuleFor(c => c.Description)
			.MaximumLength(1000)
			.When(c => !string.IsNullOrEmpty(c.Description))
			.WithMessage("Feature flag description cannot exceed 1000 characters");
	}
}