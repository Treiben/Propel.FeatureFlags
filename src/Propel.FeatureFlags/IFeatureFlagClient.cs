using Propel.FeatureFlags.Evaluation;

namespace Propel.FeatureFlags;

public interface IFeatureFlagClient
{
	Task<bool> IsEnabledAsync(string flagKey, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
	Task<T> GetVariationAsync<T>(string flagKey, T defaultValue, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
	Task<EvaluationResult?> EvaluateAsync(string flagKey, string? tenantId = null, string? userId = null, string? timeZone = null, Dictionary<string, object>? attributes = null);
}

public sealed class FeatureFlagClient(IFeatureFlagEvaluator evaluator, string? defaultTimeZone = null) : IFeatureFlagClient
{
	private readonly string? _defaultTimeZone = defaultTimeZone ?? "UTC";

	public async Task<bool> IsEnabledAsync(string flagKey, string? tenantId = null, 
		string? userId = null, Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone);

		var result = await evaluator.Evaluate(flagKey, context);
		return result!.IsEnabled;
	}

	public async Task<T> GetVariationAsync<T>(string flagKey, T defaultValue, string? tenantId = null, 
		string? userId = null, Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone);

		return await evaluator.GetVariation(flagKey, defaultValue, context);
	}

	public async Task<EvaluationResult?> EvaluateAsync(string flagKey, string? tenantId = null, 
		string? userId = null, string? timeZone = null, Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: timeZone ?? _defaultTimeZone);

		return await evaluator.Evaluate(flagKey, context);
	}
}