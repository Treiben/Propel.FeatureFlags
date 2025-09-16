using Microsoft.AspNetCore.Http;
using Propel.FeatureFlags.Services.ApplicationScope;

namespace Propel.FeatureFlags.AspNetCore.Extensions;

public static class HttpContextExtensions
{
	public static HttpContextFeatureFlagEvaluator? FeatureFlags(this HttpContext context)
	{
		return context.Items["FeatureFlagEvaluator"] as HttpContextFeatureFlagEvaluator;
	}

	public static async Task<bool> IsFeatureFlagEnabledAsync(this HttpContext context, IRegisteredFeatureFlag flag)
	{
		var evaluator = context.FeatureFlags();
		if (evaluator == null)
			throw new InvalidOperationException("Feature flag middleware not configured. Add UseFeatureFlags() to your pipeline.");

		return await evaluator.IsEnabledAsync(flag);
	}

	public static async Task<T> GetFeatureFlagVariationAsync<T>(this HttpContext context, IRegisteredFeatureFlag flag, T defaultValue)
	{
		var evaluator = context.FeatureFlags();
		if (evaluator == null)
			throw new InvalidOperationException("Feature flag middleware not configured. Add UseFeatureFlags() to your pipeline.");

		return await evaluator.GetVariationAsync(flag, defaultValue);
	}
}
