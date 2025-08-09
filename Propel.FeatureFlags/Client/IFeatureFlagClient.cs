namespace Propel.FeatureFlags.Client;

public interface IFeatureFlagClient
{
	Task<bool> IsEnabledAsync(string flagKey, string? userId = null, Dictionary<string, object>? attributes = null);
	Task<T> GetVariationAsync<T>(string flagKey, T defaultValue, string? userId = null, Dictionary<string, object>? attributes = null);
	Task<EvaluationResult> EvaluateAsync(string flagKey, string? userId = null, Dictionary<string, object>? attributes = null);
}

public sealed class FeatureFlagClient(IFeatureFlagEvaluator evaluator, string? defaultTimeZone = null) : IFeatureFlagClient
{
	private readonly string? _defaultTimeZone = defaultTimeZone ?? "UTC";

	public async Task<bool> IsEnabledAsync(string flagKey, string? userId = null, Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(userId: userId, attributes: attributes, timeZone: _defaultTimeZone);

		var result = await evaluator.EvaluateAsync(flagKey, context);
		return result.IsEnabled;
	}

	public async Task<T> GetVariationAsync<T>(string flagKey, T defaultValue, string? userId = null, Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(userId: userId, attributes: attributes, timeZone: _defaultTimeZone);

		return await evaluator.GetVariationAsync(flagKey, defaultValue, context);
	}

	public async Task<EvaluationResult> EvaluateAsync(string flagKey, string? userId = null, Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(userId: userId, attributes: attributes, timeZone: _defaultTimeZone);

		return await evaluator.EvaluateAsync(flagKey, context);
	}
}