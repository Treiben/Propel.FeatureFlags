using FeatureRabbit.Flags.Client;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Management.Api.Endpoints.Shared;
using FluentValidation;

namespace FeatureRabbit.Management.Api.Endpoints;

public record EvaluateMultipleRequest
{
	public List<string> FlagKeys { get; set; } = [];
	public string? UserId { get; set; }
	public Dictionary<string, object>? Attributes { get; set; }
}

public sealed class EvaluateMultipleEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/evaluate", 
			async (
				EvaluateMultipleRequest request,
				MultiFlagEvaluatorHandler multiFlagEvaluatorHandler) =>
			{
				return await multiFlagEvaluatorHandler.HandleAsync(request);
			})
			.AddEndpointFilter<ValidationFilter<EvaluateMultipleRequestValidator>>()
			.WithName("EvaluateMultipleFeatureFlags")
			.WithTags("Feature Flags", "Evaluation", "Client API")
			.Produces<Dictionary<string, EvaluationResult>>()
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
	}

	public sealed class EvaluateMultipleRequestValidator : AbstractValidator<EvaluateMultipleRequest>
	{
		public EvaluateMultipleRequestValidator()
		{
			RuleFor(c => c.FlagKeys).NotEmpty()
				.WithMessage("FlagKeys must be provided.");
		}
	}
}

public sealed class MultiFlagEvaluatorHandler(IFeatureFlagClient client,
				ILogger<MultiFlagEvaluatorHandler> logger)
{
	public async Task<IResult> HandleAsync(EvaluateMultipleRequest request)
	{
		try
		{
			var results = new Dictionary<string, EvaluationResult>();
			foreach (var flagKey in request.FlagKeys)
			{
				results[flagKey] = await client.EvaluateAsync(flagKey, request.UserId, request.Attributes);
			}

			return Results.Ok(results);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error evaluating multiple flags");
			return Results.StatusCode(500);
		}
	}
}

public sealed class EvaluateFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapGet("/api/feature-flags/evaluate/{key}",
			async (
				string key,
				string? userId,
				string? attributes,
				EvaluationHandler evaluationHandler) =>
			{
				return await evaluationHandler.HandleAsync(key, userId, attributes);
			})
			.WithName("EvaluateFeatureFlag")
			.WithTags("Feature Flags", "Evaluation", "Client API")
			.Produces<EvaluationResult>()
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
	}
}

public sealed class EvaluationHandler(IFeatureFlagClient client,
				ILogger<EvaluationHandler> logger)
{
	public async Task<IResult> HandleAsync(string key, string? userId, string? attributes)
	{
		try
		{
			var attributeDict = new Dictionary<string, object>();
			if (!string.IsNullOrEmpty(attributes))
			{
				if (!SerializationHelpers.TryDeserialize(attributes, out Dictionary<string, object>? deserializedAttributes))
				{
					logger.LogWarning("Failed to deserialize attributes for flag {Key}", key);
					return Results.BadRequest("Invalid attributes format. Expected a valid JSON object.");
				}
				else
				{
					attributeDict = deserializedAttributes;
				}
			}
			var result = await client.EvaluateAsync(key, userId, attributeDict);
			return Results.Ok(result);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error evaluating flag {Key}", key);
			return Results.StatusCode(500);
		}
	}
}