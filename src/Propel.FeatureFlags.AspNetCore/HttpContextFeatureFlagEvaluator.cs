using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.AspNetCore;

public class HttpContextFeatureFlagEvaluator(
	IApplicationFlagClient client,
	string? tenantId, 
	string? userId,
	Dictionary<string, object> attributes)
{
	public async Task<bool> IsEnabledAsync(IFeatureFlag flag)
	{
		return await client.IsEnabledAsync(
			flag: flag, 
			tenantId: tenantId, 
			userId: userId,
			attributes: attributes);
	}

	public async Task<T> GetVariationAsync<T>(IFeatureFlag flag, T defaultValue)
	{
		return await client.GetVariationAsync(
			flag: flag,
			defaultValue: defaultValue, 
			tenantId: tenantId, 
			userId: userId,
			attributes: attributes); 
	}
}
