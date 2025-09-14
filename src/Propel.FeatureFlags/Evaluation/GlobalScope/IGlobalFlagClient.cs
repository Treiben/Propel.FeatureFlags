using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.GlobalScope;

public interface IGlobalFlagClient
{
	Task<bool> IsEnabledAsync(string flagKey, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
	Task<EvaluationResult?> EvaluateAsync(string flagKey, string? tenantId = null, string? userId = null, string? timeZone = null, Dictionary<string, object>? attributes = null);
}

public sealed class GlobalFlagClient(
	IGlobalFlagEvaluator evaluator,
	string? defaultTimeZone = null) : IGlobalFlagClient
{
	private readonly string? _defaultTimeZone = defaultTimeZone ?? "UTC";

	public async Task<bool> IsEnabledAsync(
		string flagKey,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone);

		var result = await evaluator.Evaluate(flagKey, context);
		return result!.IsEnabled;
	}

	public async Task<EvaluationResult?> EvaluateAsync(
		string flagKey,
		string? tenantId = null,
		string? userId = null,
		string? timeZone = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: timeZone ?? _defaultTimeZone);

		return await evaluator.Evaluate(flagKey, context);
	}
}
