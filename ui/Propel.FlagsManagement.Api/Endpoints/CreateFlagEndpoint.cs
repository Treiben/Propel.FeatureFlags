using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record CreateFeatureFlagRequest
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public DateTime? ExpirationDate { get; set; }
	public Dictionary<string, string>? Tags { get; set; }
	public bool IsPermanent { get; set; } = false;
}

public sealed class CreateFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder app)
	{

		app.MapPost("/api/feature-flags",
			async (CreateFeatureFlagRequest request,
					CreateFlagHandler createFlagHandler) =>
		{
			return await createFlagHandler.HandleAsync(request);
		})
		.AddEndpointFilter<ValidationFilter<CreateFeatureFlagRequest>>()
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.WithName("CreateFeatureFlag")
		.WithTags("Feature Flags", "CRUD Operations", "Create", "Management Api")
		.Produces<FeatureFlagResponse>(StatusCodes.Status201Created);
	}
}

public sealed class CreateFlagHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<CreateFlagHandler> logger)
{
	public async Task<IResult> HandleAsync(CreateFeatureFlagRequest request)
	{
		try
		{
			var existingFlag = await repository.GetAsync(request.Key);
			if (existingFlag != null)
			{
				return HttpProblemFactory.Conflict($"A feature flag with the key '{request.Key}' already exists. Please use a different key or update the existing flag.",
					logger);
			}

			var flag = CreateFlag(request, currentUserService.UserName!);

			var createdFlag = await repository.CreateAsync(flag);

			logger.LogInformation("Feature flag {Key} created successfully by {User}",
				flag.Key, currentUserService.UserName);

			return Results.Created($"/api/flags/{flag.Key}", new FeatureFlagResponse(createdFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}

	private static FeatureFlag CreateFlag(CreateFeatureFlagRequest source, string createdBy)
	{
		return new FeatureFlag
		{
			Key = source.Key,
			Name = source.Name,
			Description = source.Description ?? string.Empty,
			Tags = source.Tags ?? [],
			AuditRecord = FlagAuditRecord.NewFlag(createdBy),
			Lifecycle = new FlagLifecycle(isPermanent: source.IsPermanent, expirationDate: source.ExpirationDate)
		};
	}
}

public sealed class CreateFlagRequestValidator : AbstractValidator<CreateFeatureFlagRequest>
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

		RuleFor(c => c.ExpirationDate)
			.GreaterThan(DateTime.UtcNow)
			.When(c => c.ExpirationDate.HasValue)
			.WithMessage("Expiration date must be in the future");
	}
}