using Microsoft.AspNetCore.Http;
using Propel.FeatureFlags.Evaluation.ApplicationScope;

namespace Propel.FeatureFlags.AspNetCore.Extensions;

public static class HttpContextExtensions
{
	public static HttpContextFeatureFlagEvaluator FeatureFlags(this HttpContext context)
	{
		return (HttpContextFeatureFlagEvaluator)context.Items["FeatureFlagEvaluator"]!;
	}

	public static async Task<bool> IsFeatureFlagEnabledAsync(this HttpContext context, IApplicationFeatureFlag flag)
	{
		return await context.FeatureFlags().IsEnabledAsync(flag);
	}

	public static async Task<T> GetFeatureFlagVariationAsync<T>(this HttpContext context, IApplicationFeatureFlag flag, T defaultValue)
	{
		return await context.FeatureFlags().GetVariationAsync(flag, defaultValue);
	}
}
