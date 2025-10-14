using Propel.FeatureFlags.Utilities;

namespace Propel.FeatureFlags.Domain;

/// <summary>
/// Represents a collection of variations, typically used for feature flagging or A/B testing scenarios.
/// </summary>
/// <remarks>This class allows managing a set of named variations, with a designated default variation.  It
/// provides functionality to consistently select a variation for a given key and identifier,  ensuring deterministic
/// assignment based on a hash-based algorithm. </remarks>
public class Variations
{
	public Dictionary<string, object> Values { get; set; } = [];
	public string DefaultVariation { get; set; } = "";

	/// <summary>
	/// Selects a variation for the given key and identifier, ensuring consistent assignment across calls.
	/// </summary>
	/// <remarks>This method ensures that the same <paramref name="id"/> will always be assigned the same variation
	/// for a given <paramref name="key"/>. Variations are selected from the set of eligible variations, excluding the
	/// default variation.</remarks>
	/// <param name="key">A unique key representing the context or feature for which the variation is being selected.</param>
	/// <param name="id">A unique identifier, such as a user ID, used to ensure consistent variation assignment for the same entity.</param>
	/// <returns>The selected variation as a string. If no eligible variations are available, the default variation is returned.</returns>
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

	public override bool Equals(object obj)
	{
		return DefaultVariation == (obj as Variations)?.DefaultVariation
			&& Values.Count == (obj as Variations)?.Values.Count
			&& Values.All(kv => (obj as Variations)?.Values.ContainsKey(kv.Key) == true
				&& (obj as Variations)?.Values[kv.Key]?.ToString() == kv.Value?.ToString());
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Values, DefaultVariation);
	}
}
