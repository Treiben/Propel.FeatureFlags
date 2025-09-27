using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluationServices.ApplicationScope;

public interface IFeatureFlagClient
{
	Task<bool> IsEnabledAsync(IFeatureFlag flag, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);

	Task<T> GetVariationAsync<T>(IFeatureFlag flag, T defaultValue, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);

	Task<EvaluationResult?> EvaluateAsync(IFeatureFlag flag, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
}

public sealed class FeatureFlagClient(
	IFeatureFlagEvaluator evaluator) : IFeatureFlagClient
{
	public async Task<bool> IsEnabledAsync(
		IFeatureFlag flag,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		var result = await evaluator.Evaluate(flag, context);
		return result!.IsEnabled;
	}

	public async Task<T> GetVariationAsync<T>(
		IFeatureFlag flag,
		T defaultValue,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		return await evaluator.GetVariation(flag, defaultValue, context);
	}

	public async Task<EvaluationResult?> EvaluateAsync(
		IFeatureFlag flag,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		return await evaluator.Evaluate(flag, context);
	}
}
