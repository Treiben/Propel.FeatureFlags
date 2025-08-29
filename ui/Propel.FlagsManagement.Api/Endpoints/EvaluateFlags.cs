using FluentValidation;
using Propel.FeatureFlags.Client;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record EvaluateMultpleFlagsRequest
{
	public List<string> FlagKeys { get; set; } = [];
	public string? UserId { get; set; }
	public Dictionary<string, object>? Attributes { get; set; }
}

public sealed class EvaluateMultipleFlagsEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/evaluate", 
			async (
				EvaluateMultpleFlagsRequest request,
				MultiFlagEvaluatorHandler multiFlagEvaluatorHandler) =>
			{
				return await multiFlagEvaluatorHandler.HandleAsync(request);
			})
			.AddEndpointFilter<ValidationFilter<EvaluateMultipleRequestValidator>>()
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
			.WithName("EvaluateMultipleFeatureFlags")
			.WithTags("Feature Flags", "Evaluations", "Management Api")
			.Produces<Dictionary<string, EvaluationResult>>()
			.ProducesValidationProblem();
	}
}

public sealed class MultiFlagEvaluatorHandler(IFeatureFlagClient client,
				ILogger<MultiFlagEvaluatorHandler> logger)
{
	public async Task<IResult> HandleAsync(EvaluateMultpleFlagsRequest request)
	{
		try
		{
			var results = new Dictionary<string, EvaluationResult>();
			foreach (var flagKey in request.FlagKeys)
			{
				results[flagKey] = await client.EvaluateAsync(flagKey: flagKey, userId: request.UserId, attributes: request.Attributes);
			}

			return Results.Ok(results);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class EvaluateMultipleRequestValidator : AbstractValidator<EvaluateMultpleFlagsRequest>
{
	public EvaluateMultipleRequestValidator()
	{
		RuleFor(c => c.FlagKeys).NotEmpty()
			.WithMessage("FlagKeys must be provided.");
	}
}