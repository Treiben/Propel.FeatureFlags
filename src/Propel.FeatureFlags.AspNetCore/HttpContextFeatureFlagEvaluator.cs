using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.AspNetCore;

public class HttpContextFeatureFlagEvaluator(IFeatureFlagClient client, string? tenantId, string? userId, Dictionary<string, object> attributes)
{
	public async Task<bool> IsEnabledAsync(string flagKey, bool enableOnCreate = false)
	{
		return await client.IsEnabledAsync(
			flagKey: flagKey, 
			tenantId: tenantId, 
			userId: userId,
			attributes: attributes, 
			enableOnCreate: enableOnCreate);
	}

	public async Task<bool> IsEnabledAsync(ITypeSafeFeatureFlag flag)
	{
		return await client.IsEnabledAsync(flag: flag, tenantId: tenantId, userId: userId, attributes: attributes);
	}

	public async Task<T> GetVariationAsync<T>(string flagKey, T defaultValue, bool enableOnCreate = false)
	{
		return await client.GetVariationAsync(
			flagKey: flagKey,
			defaultValue: defaultValue, 
			tenantId: tenantId, 
			userId: userId,
			attributes: attributes,
			enableOnCreate: enableOnCreate); 
	}

	public async Task<T> GetVariationAsync<T>(ITypeSafeFeatureFlag flag, T defaultValue)
	{
		return await client.GetVariationAsync(flag: flag, defaultValue: defaultValue, tenantId: tenantId, userId: userId, attributes: attributes);
	}
}
