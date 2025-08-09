namespace Propel.FeatureFlags.Attributes;

public class FeatureFlaggedAttribute : Attribute
{
	public string FlagKey { get; }
	public string FallbackMethod { get; }

	public FeatureFlaggedAttribute(string flagKey, string fallbackMethod = null)
	{
		FlagKey = flagKey;
		FallbackMethod = fallbackMethod;
	} 
}
