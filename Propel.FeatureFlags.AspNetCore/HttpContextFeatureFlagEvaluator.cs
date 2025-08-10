using Propel.FeatureFlags.Client;

namespace Propel.FeatureFlags.AspNetCore;

public class HttpContextFeatureFlagEvaluator
{
	private readonly IFeatureFlagClient _client;
	private readonly string ? _tenantId = null; // Assuming tenant ID is not used in this context
	private readonly string? _userId;
	private readonly Dictionary<string, object> _attributes;

	public HttpContextFeatureFlagEvaluator(IFeatureFlagClient client, string? userId, Dictionary<string, object> attributes)
	{
		_client = client;
		_userId = userId;
		_attributes = attributes;
	}

	public async Task<bool> IsEnabledAsync(string flagKey)
	{
		return await _client.IsEnabledAsync(flagKey, _tenantId, _userId, _attributes);
	}

	public async Task<T> GetVariationAsync<T>(string flagKey, T defaultValue)
	{
		return await _client.GetVariationAsync(flagKey, defaultValue, _tenantId, _userId, _attributes);
	}
}
