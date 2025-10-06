using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Clients;

public interface IGlobalFlagClient
{
	Task<bool> IsEnabledAsync(string flagKey, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
	Task<EvaluationResult?> EvaluateAsync(string flagKey, string? tenantId = null, string? userId = null, Dictionary<string, object>? attributes = null);
}

public sealed class GlobalFlagClient(
	IGlobalFlagProcessor processor) : IGlobalFlagClient
{
	public async Task<bool> IsEnabledAsync(
		string flagKey,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		var result = await processor.Evaluate(flagKey, context);
		return result!.IsEnabled;
	}

	public async Task<EvaluationResult?> EvaluateAsync(
		string flagKey,
		string? tenantId = null,
		string? userId = null,
		Dictionary<string, object>? attributes = null)
	{
		var context = new EvaluationContext(
			tenantId: tenantId,
			userId: userId,
			attributes: attributes);

		return await processor.Evaluate(flagKey, context);
	}
}
