namespace Propel.FeatureFlags.Core;

public class TargetingRule
{
	public string Attribute { get; set; } = string.Empty;
	public TargetingOperator Operator { get; set; }
	public List<string> Values { get; set; } = [];
	public string Variation { get; set; } = "on";
}

public enum TargetingOperator
{
	Equals,
	NotEquals,
	Contains,
	NotContains,
	In,
	NotIn,
	GreaterThan,
	LessThan
}
