using Propel.FeatureFlags.Evaluation.ApplicationScope;

namespace Propel.FeatureFlags.Attributes
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public class FeatureFlaggedAttribute(string flagKey, string? fallbackMethod = null) : Attribute
	{
		public string FlagKey { get; } = flagKey;
		public string? FallbackMethod { get; } = fallbackMethod;
	}

	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public class FeatureFlaggedV2Attribute(Type type, string? fallbackMethod = null) : Attribute
	{
		public Type FlagType { get; } = type;
		public string? FallbackMethod { get; } = fallbackMethod;
	}
}
