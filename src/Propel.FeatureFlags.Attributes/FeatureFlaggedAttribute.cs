namespace Propel.FeatureFlags.Attributes
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public class FeatureFlaggedAttribute(string flagKey, string? fallbackMethod = null) : Attribute
	{
		public string FlagKey { get; } = flagKey;
		public string? FallbackMethod { get; } = fallbackMethod;
	}
}
