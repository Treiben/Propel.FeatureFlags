using Microsoft.AspNetCore.Mvc;

namespace Propel.FeatureFlags.AspNetCore.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="ControllerBase"/> class.
/// </summary>
/// <remarks>This class contains methods that extend the functionality of <see cref="ControllerBase"/> to simplify
/// common operations, such as accessing feature flag evaluators from the HTTP context.</remarks>
public static class ControllerExtensions
{
	/// <summary>
	/// Retrieves the <see cref="HttpContextFeatureFlagEvaluator"/> instance associated with the current HTTP context.
	/// </summary>
	/// <param name="controller">The controller from which to retrieve the feature flag evaluator.</param>
	/// <returns>The <see cref="HttpContextFeatureFlagEvaluator"/> instance stored in the HTTP context's items collection.</returns>
	public static HttpContextFeatureFlagEvaluator FeatureFlags(this ControllerBase controller)
	{
		return (HttpContextFeatureFlagEvaluator)controller.HttpContext.Items["FeatureFlagEvaluator"]!;
	}
}
