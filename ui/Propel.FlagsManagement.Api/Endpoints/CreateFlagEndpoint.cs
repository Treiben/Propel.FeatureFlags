using FluentValidation;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

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
		.WithTags("Feature Flags", "CRUD Operations", "Create", "Management Api")
		.Produces<FeatureFlagResponse>(StatusCodes.Status201Created);
	}
}

public sealed class CreateGlobalFlagHandler(
		IFlagManagementRepository repository,
		ICurrentUserService currentUserService,
		ILogger<CreateGlobalFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(CreateGlobalFeatureFlagRequest request, CancellationToken cancellationToken)
	{
		try
		{
			var flagKey = new FlagKey(request.Key, Scope.Global);
			var globalFlag = FeatureFlag.Create(key: flagKey,
				name: request.Name,
				description: request.Description ?? string.Empty);

			globalFlag.Retention = RetentionPolicy.Global;
			globalFlag.Tags = request.Tags ?? [];
			globalFlag.Created = AuditTrail.FlagCreated(currentUserService.UserName!);

			logger.LogInformation("Feature flag {Key} created successfully by {User}",
				globalFlag.Key, currentUserService.UserName);

			var flag = await repository.CreateAsync(globalFlag, cancellationToken);

			return Results.Created($"/api/flags/{globalFlag.Key.Key}", new FeatureFlagResponse(globalFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
		}
		catch (DuplicatedFeatureFlagException ex)
		{
			return HttpProblemFactory.Conflict(
				$"A feature flag with the key '{request.Key}' already exists. Please use a different key or update the existing flag.", 
				logger);
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