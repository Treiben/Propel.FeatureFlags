namespace Propel.FeatureFlags.Core
{
	public class TargetingRule
	{
		public string Attribute { get; set; } = string.Empty;
		public TargetingOperator Operator { get; set; }
		public List<string> Values { get; set; } = new List<string>();
		public string Variation { get; set; } = "on";
	}
}
