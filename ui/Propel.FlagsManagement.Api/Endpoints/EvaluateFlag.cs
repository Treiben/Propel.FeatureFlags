using Propel.FeatureFlags.Client;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

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
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
			.WithName("EvaluateFeatureFlag")
			.WithTags("Feature Flags", "Evaluations", "Management Api")
			.Produces<EvaluationResult>();
	}
}

public sealed class EvaluationHandler(
	IFeatureFlagClient client,
	ILogger<EvaluationHandler> logger)
{
	public async Task<IResult> HandleAsync(string key, string? userId, string? attributes)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		// Validate and parse attributes
		var attributeDict = new Dictionary<string, object>();
		if (!string.IsNullOrEmpty(attributes))
		{
			if (!SerializationHelpers.TryDeserialize(attributes, out Dictionary<string, object>? deserializedAttributes))
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
			var result = await client.EvaluateAsync(flagKey: key, userId: userId, attributes: attributeDict);

			// Add evaluation context to the result for better debugging
			result = result.WithUser(userId);

			logger.LogDebug("Feature flag {Key} evaluated for user {UserId} with result {IsEnabled}",
				key, userId ?? "anonymous", result.IsEnabled);

			return Results.Ok(result);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}
