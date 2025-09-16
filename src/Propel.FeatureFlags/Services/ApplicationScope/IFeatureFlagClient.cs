using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Services.ApplicationScope;

public interface IFeatureFlagClient
{
	Task<bool> IsEnabledAsync(IRegisteredFeatureFlag flag, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);

	Task<T> GetVariationAsync<T>(IRegisteredFeatureFlag flag, T defaultValue, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);

	Task<EvaluationResult?> EvaluateAsync(IRegisteredFeatureFlag flag, string? tenantId = null, string? userId = null, string? timeZone = null, Dictionary<string, object>? attributes = null);
}

public sealed class FeatureFlagClient(
	IFeatureFlagEvaluator evaluator,
	string? defaultTimeZone = null) : IFeatureFlagClient
{
	private readonly string? _defaultTimeZone = defaultTimeZone ?? "UTC";

	public async Task<bool> IsEnabledAsync(
		IRegisteredFeatureFlag flag,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone);

		var result = await evaluator.Evaluate(flag, context);
		return result!.IsEnabled;
	}

	public async Task<T> GetVariationAsync<T>(
		IRegisteredFeatureFlag flag,
		T defaultValue,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes,
			timeZone: _defaultTimeZone);

		return await evaluator.GetVariation(flag, defaultValue, context);
	}

	public async Task<EvaluationResult?> EvaluateAsync(
		IRegisteredFeatureFlag flag,
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

		return await evaluator.Evaluate(flag, context);
	}
}
