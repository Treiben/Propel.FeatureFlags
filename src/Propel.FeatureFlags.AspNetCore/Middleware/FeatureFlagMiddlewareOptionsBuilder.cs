using Microsoft.AspNetCore.Http;

namespace Propel.FeatureFlags.AspNetCore.Middleware;

public class FeatureFlagMiddlewareOptionsBuilder
{
	private readonly FeatureFlagMiddlewareOptions _options = new();

	public FeatureFlagMiddlewareOptionsBuilder EnableMaintenance(string flagKey = "maintenance-mode")
	{
		_options.EnableMaintenanceMode = true;
		_options.MaintenanceFlagKey = flagKey;
		return this;
	}

	public FeatureFlagMiddlewareOptionsBuilder DisableMaintenance()
	{
		_options.EnableMaintenanceMode = false;
		return this;
	}

	public FeatureFlagMiddlewareOptionsBuilder WithMaintenanceResponse(object response)
	{
		_options.MaintenanceResponse = response;
		return this;
	}

	public FeatureFlagMiddlewareOptionsBuilder AddGlobalFlag(string flagKey, int statusCode, object response)
	{
		_options.GlobalFlags.Add(new GlobalFlag
		{
			Key = flagKey,
			StatusCode = statusCode,
			Response = response
		});
		return this;
	}

	public FeatureFlagMiddlewareOptionsBuilder ExtractTenantIdFrom(Func<HttpContext, string?> extractor)
	{
		_options.TenantIdExtractor = extractor;
		return this;
	}

	public FeatureFlagMiddlewareOptionsBuilder ExtractUserIdFrom(Func<HttpContext, string?> extractor)
	{
		_options.UserIdExtractor = extractor;
		return this;
	}

	public FeatureFlagMiddlewareOptionsBuilder ExtractAttributes(Func<HttpContext, Dictionary<string, object>> extractor)
	{
		_options.AttributeExtractors.Add(extractor);
		return this;
	}

	public FeatureFlagMiddlewareOptions Build() => _options;
}
