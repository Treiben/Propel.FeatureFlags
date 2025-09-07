using Propel.FeatureFlags;
using Propel.FeatureFlags.Evaluation;

namespace FeatureFlags.UnitTests.Client;

public class FeatureFlagClient_AttributesHandling
{
	private readonly Mock<IFeatureFlagEvaluator> _mockEvaluator;
	private readonly FeatureFlagClient _client;

	public FeatureFlagClient_AttributesHandling()
	{
		_mockEvaluator = new Mock<IFeatureFlagEvaluator>();
		_client = new FeatureFlagClient(_mockEvaluator.Object);
	}

	[Fact]
	public async Task If_AttributesWithNullValues_ThenPassesThrough()
	{
		// Arrange
		var flagKey = "null-attributes-flag";
		string tenantId = null!;
		var userId = "user123";
		var attributes = new Dictionary<string, object>
		{
			{ "key1", "value1" },
			{ "key2", null! },
			{ "key3", "value3" }
		};
		var expectedResult = new EvaluationResult(isEnabled: true);

		_mockEvaluator.Setup(x => x.Evaluate(
			flagKey,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedResult);

		// Act
		await _client.IsEnabledAsync(flagKey, tenantId, userId, attributes);

		// Assert
		_mockEvaluator.Verify(x => x.Evaluate(
			flagKey,
			It.Is<EvaluationContext>(ctx =>
				ctx.Attributes.Count == 3 &&
				ctx.Attributes["key1"].ToString() == "value1" &&
				ctx.Attributes["key2"] == null &&
				ctx.Attributes["key3"].ToString() == "value3"),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task If_AttributesWithComplexObjects_ThenPassesThrough()
 	{
		// Arrange
		var flagKey = "complex-attributes-flag";
		string tenantId = null!;
		var userId = "user123";
		var complexObject = new { Name = "Test", Count = 42 };
		var attributes = new Dictionary<string, object>
		{
			{ "simple", "value" },
			{ "complex", complexObject },
			{ "number", 123 }
		};
		var expectedResult = new EvaluationResult(isEnabled: true);

		_mockEvaluator.Setup(x => x.Evaluate(
			flagKey,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedResult);

		// Act
		await _client.IsEnabledAsync(flagKey, tenantId, userId, attributes);

		// Assert
		_mockEvaluator.Verify(x => x.Evaluate(
			flagKey,
			It.Is<EvaluationContext>(ctx =>
				ctx.Attributes.Count == 3 &&
				ctx.Attributes["simple"].ToString() == "value" &&
				ctx.Attributes["complex"] == complexObject &&
				ctx.Attributes["number"].Equals(123)),
			It.IsAny<CancellationToken>()), Times.Once);
	}
}

public class FeatureFlagClient_StringHandling
{
	private readonly Mock<IFeatureFlagEvaluator> _mockEvaluator;
	private readonly FeatureFlagClient _client;

	public FeatureFlagClient_StringHandling()
	{
		_mockEvaluator = new Mock<IFeatureFlagEvaluator>();
		_client = new FeatureFlagClient(_mockEvaluator.Object);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("valid-flag-key")]
	[InlineData("flag_with_underscores")]
	[InlineData("flag-with-dashes")]
	[InlineData("123-numeric-start")]
	public async Task If_DifferentFlagKeyFormats_ThenPassesThrough(string flagKey)
	{
		// Arrange
		var userId = "user123";
		var expectedResult = new EvaluationResult(isEnabled: true);

		_mockEvaluator.Setup(x => x.Evaluate(
			flagKey,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedResult);

		// Act
		var result = await _client.IsEnabledAsync(flagKey, userId);

		// Assert
		result.ShouldBeTrue();
		_mockEvaluator.Verify(x => x.Evaluate(
			flagKey,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("user123")]
	[InlineData("user@example.com")]
	[InlineData("user-with-special-chars!@#")]
	public async Task If_DifferentUserIdFormats_ThenPassesThrough(string userId)
	{
		// Arrange
		var flagKey = "test-flag";
		var expectedResult = new EvaluationResult(isEnabled: true);

		_mockEvaluator.Setup(x => x.Evaluate(
			flagKey,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedResult);

		// Act
		var result = await _client.IsEnabledAsync(flagKey: flagKey, userId: userId);

		// Assert
		result.ShouldBeTrue();
		_mockEvaluator.Verify(x => x.Evaluate(
			flagKey,
			It.Is<EvaluationContext>(ctx => ctx.UserId == userId),
			It.IsAny<CancellationToken>()), Times.Once);
	}
}

public class FeatureFlagClient_VariationTypes
{
	private readonly Mock<IFeatureFlagEvaluator> _mockEvaluator;
	private readonly FeatureFlagClient _client;

	public FeatureFlagClient_VariationTypes()
	{
		_mockEvaluator = new Mock<IFeatureFlagEvaluator>();
		_client = new FeatureFlagClient(_mockEvaluator.Object);
	}

	[Fact]
	public async Task If_BooleanVariation_ThenReturnsCorrectType()
	{
		// Arrange
		var flagKey = "bool-flag";
		var defaultValue = false;
		var expectedValue = true;

		_mockEvaluator.Setup(x => x.GetVariation(
			flagKey,
			defaultValue,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedValue);

		// Act
		var result = await _client.GetVariationAsync(flagKey, defaultValue);

		// Assert
		result.ShouldBe(expectedValue);
		result.ShouldBeOfType<bool>();
	}

	[Fact]
	public async Task If_DoubleVariation_ThenReturnsCorrectType()
	{
		// Arrange
		var flagKey = "double-flag";
		var defaultValue = 0.0;
		var expectedValue = 3.14159;

		_mockEvaluator.Setup(x => x.GetVariation(
			flagKey,
			defaultValue,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedValue);

		// Act
		var result = await _client.GetVariationAsync(flagKey, defaultValue);

		// Assert
		result.ShouldBe(expectedValue);
		result.ShouldBeOfType<double>();
	}

	[Fact]
	public async Task If_ListVariation_ThenReturnsCorrectType()
	{
		// Arrange
		var flagKey = "list-flag";
		var defaultValue = new List<string>();
		var expectedValue = new List<string> { "item1", "item2", "item3" };

		_mockEvaluator.Setup(x => x.GetVariation(
			flagKey,
			defaultValue,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedValue);

		// Act
		var result = await _client.GetVariationAsync(flagKey, defaultValue);

		// Assert
		result.ShouldBe(expectedValue);
		result.Count.ShouldBe(3);
		result[0].ShouldBe("item1");
	}

	[Fact]
	public async Task If_DictionaryVariation_ThenReturnsCorrectType()
	{
		// Arrange
		var flagKey = "dict-flag";
		var defaultValue = new Dictionary<string, int>();
		var expectedValue = new Dictionary<string, int>
		{
			{ "key1", 1 },
			{ "key2", 2 }
		};

		_mockEvaluator.Setup(x => x.GetVariation(
			flagKey,
			defaultValue,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedValue);

		// Act
		var result = await _client.GetVariationAsync(flagKey, defaultValue);

		// Assert
		result.ShouldBe(expectedValue);
		result.Count.ShouldBe(2);
		result["key1"].ShouldBe(1);
		result["key2"].ShouldBe(2);
	}
}

public class FeatureFlagClient_InterfaceCompliance
{
	private readonly Mock<IFeatureFlagEvaluator> _mockEvaluator;
	private readonly FeatureFlagClient _client;

	public FeatureFlagClient_InterfaceCompliance()
	{
		_mockEvaluator = new Mock<IFeatureFlagEvaluator>();
		_client = new FeatureFlagClient(_mockEvaluator.Object);
	}

	[Fact]
	public async Task If_UsedThroughInterface_ThenWorksCorrectly()
	{
		// Arrange
		var flagKey = "interface-flag";
		var userId = "user123";
		var expectedResult = new EvaluationResult(isEnabled: true);

		_mockEvaluator.Setup(x => x.Evaluate(
			flagKey,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()))
			.ReturnsAsync(expectedResult);

		// Act
		var isEnabledResult = await _client.IsEnabledAsync(flagKey, userId);
		var variationResult = await _client.GetVariationAsync(flagKey, "default", userId);
		var evaluateResult = await _client.EvaluateAsync(flagKey, userId);

		// Assert
		isEnabledResult.ShouldBeTrue();
		evaluateResult.ShouldBe(expectedResult);

		_mockEvaluator.Verify(x => x.Evaluate(
			flagKey,
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()), Times.Exactly(2));

		_mockEvaluator.Verify(x => x.GetVariation(
			flagKey,
			"default",
			It.IsAny<EvaluationContext>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}
}
