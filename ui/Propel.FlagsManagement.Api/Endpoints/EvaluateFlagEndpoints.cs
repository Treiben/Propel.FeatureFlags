using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Evaluation;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record EvaluateFeatureFlagsRequest
{
	public string[] Keys { get; set; } = [];
	public string? UserId { get; set; }
	public Dictionary<string, object>? Attributes { get; set; }
}

public sealed class EvaluateFlagEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapGet("/api/feature-flags/evaluate/{key}",
			async (
				string key,
				string? userId,
				string? kvAttributes,
				FlagEvaluationHandler evaluationHandler) =>
			{
				if (string.IsNullOrWhiteSpace(key))
				{
					return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null");
				}

				return await evaluationHandler.HandleAsync(keys: [key], userId: userId, kvAttributes: kvAttributes);
			})
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
			.WithName("EvaluateFeatureFlag")
			.WithTags("Feature Flags", "Evaluations", "Management Api")
			.Produces<EvaluationResult>();

		epRoutBuilder.MapPost("/api/feature-flags/evaluate",
			async (
				EvaluateFeatureFlagsRequest request,
				FlagEvaluationHandler evaluationHandler) =>
			{
				return await evaluationHandler.HandleAsync(keys: request.Keys, userId: request.UserId, attributes: request.Attributes);
			})
			.AddEndpointFilter<ValidationFilter<EvaluateMultipleRequestValidator>>()
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
			.WithName("EvaluateMultipleFeatureFlags")
			.WithTags("Feature Flags", "Evaluations", "Management Api")
			.Produces<Dictionary<string, EvaluationResult>>()
			.ProducesValidationProblem();
	}
}

public sealed class FlagEvaluationHandler(
	IFeatureFlagClient client,
	ILogger<FlagEvaluationHandler> logger)
{
	public async Task<IResult> HandleAsync(string[] keys, string? userId, string? kvAttributes = null, Dictionary<string, object>? attributes = null)
	{
		// Validate and parse attributes
		var attributeDict = attributes;
		if (attributeDict == null && !string.IsNullOrEmpty(kvAttributes))
		{
			if (!SerializationHelpers.TryDeserialize(kvAttributes, out Dictionary<string, object>? deserializedAttributes))
			{
				return HttpProblemFactory.BadRequest(
					"Invalid Attributes Format",
					"The attributes parameter must be a valid JSON object. Example: {\"country\":\"US\",\"plan\":\"premium\"}",
					logger);
			}
			attributeDict = deserializedAttributes ?? [];
		}

		try
		{
			var results = new Dictionary<string, EvaluationResult>();
			foreach (var key in keys)
			{
				var result = await client.EvaluateAsync(flagKey: key, userId: userId, attributes: attributeDict);
				if (result != null)
				{
					results[key] = result;
					logger.LogDebug("Feature flag {Key} evaluated for user {UserId} with result {IsEnabled}", key, userId ?? "anonymous", result.IsEnabled);
				}
				else
				{
					logger.LogWarning("Feature flag {Key} failed to produce result during evaluation for user {UserId}", key, userId ?? "anonymous");
				}
			}

			return Results.Ok(results);

		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class EvaluateMultipleRequestValidator : AbstractValidator<EvaluateFeatureFlagsRequest>
{
	public EvaluateMultipleRequestValidator()
	{
		RuleFor(c => c.Keys).NotEmpty()
			.WithMessage("FlagKeys must be provided.");
	}
}
