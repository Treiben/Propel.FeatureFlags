namespace Propel.FeatureFlags.Core;

public class FlagVariations
{
	public Dictionary<string, object> Values { get; set; } = [];
	public string DefaultVariation { get; set; } = "off";

	public static FlagVariations OnOff => new()
	{
		Values = new Dictionary<string, object>
		{
			{ "off", false },
			{ "on", true }
		},
		DefaultVariation = "off"
	};

	public string SelectVariationFor(string key, string id)
	{
		// If flag only has simple on/off variations, return "on"
		if (Values.Count <= 2 &&
			Values.ContainsKey("on") &&
			Values.ContainsKey("off"))
		{
			return "on";
		}

		// For A/B testing with multiple variations, use consistent hash-based selection
		var availableVariations = Values.Keys
			.Where(k => k != DefaultVariation) // Exclude default (usually "off" or fallback)
			.ToArray();

		if (availableVariations.Length == 0)
		{
			return	DefaultVariation;
		}

		if (availableVariations.Length == 1)
		{
			return availableVariations[0];
		}

		// Use hash-based selection for consistent variation assignment
		var hashInput = $"{key}:variation:{id}";
		var hash = Hasher.ComputeHash(hashInput);
		var variationIndex = (int)(hash % (uint)availableVariations.Length);

		return availableVariations[variationIndex];
	}
}
