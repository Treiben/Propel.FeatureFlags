using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class Variations_SelectVariationFor
{
	[Fact]
	public void SelectVariationFor_SimpleOnOffFlag_ReturnsOn()
	{
		// Arrange
		var variations = new Variations
		{
			Values = new Dictionary<string, object>
			{
				{ "on", true },
				{ "off", false }
			},
			DefaultVariation = "off"
		};

		// Act
		var result = variations.SelectVariationFor("simple-flag", "user123");

		// Assert
		result.ShouldBe("on");
	}

	[Fact]
	public void SelectVariationFor_MultipleVariations_ReturnsConsistentHashBasedSelection()
	{
		// Arrange
		var variations = new Variations
		{
			Values = new Dictionary<string, object>
			{
				{ "v1", "version1" },
				{ "v2", "version2" },
				{ "v3", "version3" },
				{ "off", false }
			},
			DefaultVariation = "off"
		};

		// Act - Same inputs should return same variation
		var result1 = variations.SelectVariationFor("checkout-version", "user123");
		var result2 = variations.SelectVariationFor("checkout-version", "user123");

		// Assert
		result1.ShouldBe(result2);
		result1.ShouldBeOneOf("v1", "v2", "v3");
		result1.ShouldNotBe("off"); // Should not return default variation
	}
}