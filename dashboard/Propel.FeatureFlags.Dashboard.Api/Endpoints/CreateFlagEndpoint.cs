using FluentValidation;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

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
		.Produces<FeatureFlagResponse>(StatusCodes.Status201Created)
		.Produces(StatusCodes.Status400BadRequest)
		.Produces(StatusCodes.Status409Conflict)
		.Produces(StatusCodes.Status500InternalServerError);
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
					"Feature Flag Already Exists",
					$"A feature flag with the key '{request.Key}' already exists. Please use a different key or update the existing flag instead.",
					logger);
			}

			var metadata = FlagAdministration.Create(
				Scope.Global,
				request.Name,
				request.Description ?? string.Empty,
				AuditTrail.FlagCreated(currentUserService.UserName!)) with { Tags = request.Tags ?? [] };

			var globalFlag = new FeatureFlag(identifier, metadata, FlagEvaluationOptions.DefaultOptions);

			var flag = await repository.CreateAsync(globalFlag, cancellationToken);

			logger.LogInformation("Feature flag {Key} created successfully by {User}",
				identifier.Key, currentUserService.UserName);

			return Results.Created($"/api/feature-flags/{globalFlag.Identifier.Key}", new FeatureFlagResponse(globalFlag));
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			return HttpProblemFactory.ClientClosedRequest(logger);
		}
		catch (ArgumentException ex)
		{
			// ArgumentException typically indicates validation or business rule violations
			return HttpProblemFactory.BadRequest(
				"Invalid Feature Flag Data",
				ex.Message,
				logger);
		}
		catch (InvalidOperationException ex)
		{
			// InvalidOperationException for state-related errors
			return HttpProblemFactory.UnprocessableEntity(
				$"Cannot create feature flag: {ex.Message}",
				logger);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Unexpected error while creating feature flag with key {Key}", request.Key);
			return HttpProblemFactory.InternalServerError(
				ex,
				logger,
				"An unexpected error occurred while creating the feature flag. Please try again or contact support if the problem persists.");
		}
	}
}

public sealed class CreateFlagRequestValidator : AbstractValidator<CreateGlobalFeatureFlagRequest>
{
	public CreateFlagRequestValidator()
	{
		RuleFor(c => c.Key)
			.NotEmpty()
			.WithMessage("Feature flag key is required and cannot be empty.");
		
		RuleFor(c => c.Key)
			.Matches(@"^[a-zA-Z0-9_-]+$")
			.WithMessage("Feature flag key can only contain letters, numbers, hyphens (-), and underscores (_).");
		
		RuleFor(c => c.Key)
			.Length(1, 100)
			.WithMessage("Feature flag key must be between 1 and 100 characters long.");

		RuleFor(c => c.Name)
			.NotEmpty()
			.WithMessage("Feature flag name is required and cannot be empty.");

		RuleFor(c => c.Name)
			.Length(1, 200)
			.WithMessage("Feature flag name must be between 1 and 200 characters long.");

		RuleFor(c => c.Description)
			.MaximumLength(1000)
			.When(c => !string.IsNullOrEmpty(c.Description))
			.WithMessage("Feature flag description cannot exceed 1000 characters.");
		
		// Validate tags if provided
		RuleFor(c => c.Tags)
			.Must(tags => tags == null || tags.Count <= 50)
			.When(c => c.Tags != null)
			.WithMessage("Feature flag cannot have more than 50 tags.");
		
		RuleForEach(c => c.Tags!.Keys)
			.Length(1, 50)
			.When(c => c.Tags != null && c.Tags.Any())
			.WithMessage("Tag keys must be between 1 and 50 characters long.");
		
		RuleForEach(c => c.Tags!.Values)
			.MaximumLength(200)
			.When(c => c.Tags != null && c.Tags.Any())
			.WithMessage("Tag values cannot exceed 200 characters.");
	}
}