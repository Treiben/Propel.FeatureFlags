namespace Propel.FeatureFlags.Core;

public class FlagVariations
{
	public Dictionary<string, object> Variations { get; set; } = [];
	public string DefaultVariation { get; set; } = "off";

	public static FlagVariations OnOff => new FlagVariations
	{
		Variations = new Dictionary<string, object>
		{
			{ "off", false },
			{ "on", true }
		},
		DefaultVariation = "off"
	};
}
