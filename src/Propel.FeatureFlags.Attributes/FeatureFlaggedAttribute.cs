namespace Propel.FeatureFlags.Attributes;

/// <summary>
/// Specifies that a method is conditionally executed based on the state of a feature flag.
/// </summary>
/// <remarks>This attribute is used to annotate methods that are gated by a feature flag. The feature flag type is
/// specified via the <see cref="FlagType"/> property, and an optional fallback method can be provided via the <see
/// cref="FallbackMethod"/> property to handle cases where the feature is disabled.</remarks>
/// <param name="type">The <see cref="Type"/> representing the feature flag. This type is expected to define the logic for determining
/// whether the feature is enabled.</param>
/// <param name="fallbackMethod">The name of an optional fallback method to invoke if the feature is disabled. This method must exist in the same
/// class as the annotated method and have a compatible signature. If null, no fallback is used.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class FeatureFlaggedAttribute(Type type, string? fallbackMethod = null) : Attribute
{
	public Type FlagType { get; } = type;
	public string? FallbackMethod { get; } = fallbackMethod;
}
