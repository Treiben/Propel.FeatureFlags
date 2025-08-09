using Microsoft.AspNetCore.Mvc;

namespace Propel.FeatureFlags.AspNetCore.Extensions;

public static class ControllerExtensions
{
	public static HttpContextFeatureFlagEvaluator FeatureFlags(this ControllerBase controller)
	{
		return (HttpContextFeatureFlagEvaluator)controller.HttpContext.Items["FeatureFlagEvaluator"]!;
	}
}
