using Propel.FeatureFlags.Helpers;

namespace Propel.FeatureFlags.Domain;

public class Variations
{
	public Dictionary<string, object> Values { get; set; } = [];
	public string DefaultVariation { get; set; } = "off";

	public static Variations OnOff => new()
	{
		Values = new Dictionary<string, object>
		{
			{ "on", true },
			{ "off", false },
		},
		DefaultVariation = "off"
	};

	public string SelectVariationFor(string key, string id)
	{
		var eligibleVariations = Values.Keys
			.Where(k => k != DefaultVariation) // Exclude default (usually "off" or fallback)
			.ToArray();

		if (eligibleVariations.Length == 0)
			return DefaultVariation;

		if (eligibleVariations.Length == 1)
			return eligibleVariations[0];

		// Use hash-based selection for consistent variation assignment
		var hashInput = $"{key}:variation:{id}";
		var hash = Hasher.ComputeHash(hashInput);
		var variationIndex = (int)(hash % (uint)eligibleVariations.Length);

		return eligibleVariations[variationIndex];
	}
}
