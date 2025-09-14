using Propel.FeatureFlags.Evaluation.ApplicationScope;

namespace Propel.FeatureFlags.AspNetCore;

public class HttpContextFeatureFlagEvaluator(IFeatureFlagClient client, string? tenantId, string? userId, Dictionary<string, object> attributes)
{
	public async Task<bool> IsEnabledAsync(IApplicationFeatureFlag flag, bool enableOnCreate = false)
	{
		return await client.IsEnabledAsync(
			flag: flag, 
			tenantId: tenantId, 
			userId: userId,
			attributes: attributes);
	}

	public async Task<bool> IsEnabledAsync(IApplicationFeatureFlag flag)
	{
		return await client.IsEnabledAsync(flag: flag, tenantId: tenantId, userId: userId, attributes: attributes);
	}

	public async Task<T> GetVariationAsync<T>(IApplicationFeatureFlag flag, T defaultValue)
	{
		return await client.GetVariationAsync(
			flag: flag,
			defaultValue: defaultValue, 
			tenantId: tenantId, 
			userId: userId,
			attributes: attributes); 
	}
}
