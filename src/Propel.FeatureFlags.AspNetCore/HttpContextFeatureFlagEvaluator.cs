namespace Propel.FeatureFlags.AspNetCore;

public class HttpContextFeatureFlagEvaluator(IFeatureFlagClient client, string? tenantId, string? userId, Dictionary<string, object> attributes)
{
	public async Task<bool> IsEnabledAsync(string flagKey)
	{
		return await client.IsEnabledAsync(flagKey: flagKey, tenantId: tenantId, userId: userId, attributes: attributes);
	}

	public async Task<T> GetVariationAsync<T>(string flagKey, T defaultValue)
	{
		return await client.GetVariationAsync(flagKey: flagKey, defaultValue: defaultValue, tenantId: tenantId, userId: userId, attributes: attributes); 
	}
}
