namespace Propel.FeatureFlags.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class FeatureFlaggedAttribute(Type type, string? fallbackMethod = null) : Attribute
{
	public Type FlagType { get; } = type;
	public string? FallbackMethod { get; } = fallbackMethod;
}
