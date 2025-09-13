using Microsoft.AspNetCore.Http;
using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.AspNetCore.Extensions;

public static class HttpContextExtensions
{
	public static HttpContextFeatureFlagEvaluator FeatureFlags(this HttpContext context)
	{
		return (HttpContextFeatureFlagEvaluator)context.Items["FeatureFlagEvaluator"]!;
	}

	public static async Task<bool> IsFeatureFlagEnabledAsync(this HttpContext context, string flagKey)
	{
		return await context.FeatureFlags().IsEnabledAsync(flagKey);
	}

	public static async Task<bool> IsFeatureFlagEnabledAsync(this HttpContext context, ITypeSafeFeatureFlag flag)
	{
		return await context.FeatureFlags().IsEnabledAsync(flag);
	}

	public static async Task<T> GetFeatureFlagVariationAsync<T>(this HttpContext context, string flagKey, T defaultValue)
	{
		return await context.FeatureFlags().GetVariationAsync(flagKey, defaultValue);
	}

	public static async Task<T> GetFeatureFlagVariationAsync<T>(this HttpContext context, ITypeSafeFeatureFlag flag, T defaultValue)
	{
		return await context.FeatureFlags().GetVariationAsync(flag, defaultValue);
	}
}
