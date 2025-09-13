using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;

namespace Propel.FeatureFlags;

public interface IFeatureFlagClient
{
	Task<bool> IsEnabledAsync(string flagKey, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null, bool enableOnCreate = false);
	Task<bool> IsEnabledAsync(ITypeSafeFeatureFlag flag, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
	Task<T> GetVariationAsync<T>(string flagKey, T defaultValue, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null, bool enableOnCreate = false);
	Task<T> GetVariationAsync<T>(ITypeSafeFeatureFlag flag, T defaultValue, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
	Task<EvaluationResult?> EvaluateAsync(string flagKey, string? tenantId = null, string? userId = null, string? timeZone = null, Dictionary<string, object>? attributes = null, bool enableOnCreation = false);
	Task<EvaluationResult?> EvaluateAsync(ITypeSafeFeatureFlag flag, string? tenantId = null, string? userId = null, string? timeZone = null, Dictionary<string, object>? attributes = null);

}

public sealed class FeatureFlagClient(IFeatureFlagEvaluator evaluator, string? defaultTimeZone = null) : IFeatureFlagClient
{
	private readonly string? _defaultTimeZone = defaultTimeZone ?? "UTC";

	public async Task<bool> IsEnabledAsync(
		string flagKey, 
		string? tenantId = null,
		string? userId = null, 
		Dictionary<string, object>? attributes = null,
		bool enableOnCreation = false)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone,
			enableOnCreation: enableOnCreation);

		var result = await evaluator.Evaluate(flagKey, context);
		return result!.IsEnabled;
	}

	public async Task<bool> IsEnabledAsync(
		ITypeSafeFeatureFlag flag, 
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone,
			enableOnCreation: flag.IsEnabledOnCreation);

		var result = await evaluator.Evaluate(flag.Key, context);
		return result!.IsEnabled;
	}

	public async Task<T> GetVariationAsync<T>(
		string flagKey,
		T defaultValue, 
		string? tenantId = null,
		string? userId = null, 
		Dictionary<string, object>? attributes = null,
		bool enableOnCreation = false)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone,
			enableOnCreation: enableOnCreation);

		return await evaluator.GetVariation(flagKey, defaultValue, context);
	}

	public async Task<T> GetVariationAsync<T>(
		ITypeSafeFeatureFlag flag, 
		T defaultValue, 
		string? tenantId = null,
		string? userId = null, 
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone,
			enableOnCreation: flag.IsEnabledOnCreation);

		return await evaluator.GetVariation(flag.Key, defaultValue, context);
	}

	public async Task<EvaluationResult?> EvaluateAsync(
		string flagKey, 
		string? tenantId = null,
		string? userId = null, 
		string? timeZone = null, 
		Dictionary<string, object>? attributes = null,
		bool enableOnCreation = false)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: timeZone ?? _defaultTimeZone,
			enableOnCreation: enableOnCreation);

		return await evaluator.Evaluate(flagKey, context);
	}

	public async Task<EvaluationResult?> EvaluateAsync(
		ITypeSafeFeatureFlag flag,
		string? tenantId = null,
		string? userId = null,
		string? timeZone = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: timeZone ?? _defaultTimeZone,
			enableOnCreation: flag.IsEnabledOnCreation);

		return await evaluator.Evaluate(flag.Key, context);
	}
}