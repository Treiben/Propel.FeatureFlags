using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.EvaluationTests.Client;

public class IsEnabledAsync_WithEnabledFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_CreateItDuringEvaluation()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("client-enabled")
			.WithEvaluationModes(EvaluationMode.Off)
			.ForFeatureFlag(defaultMode: EvaluationMode.On)
			.Build();

		// Act
		var result = await fixture.Client.IsEnabledAsync(flag, userId: "user123");

		// Assert
		result.ShouldBeTrue();

		// Verify the flag was created in the repository

		//A Arrange
		var storedFlag = await fixture.EvaluationRepository.GetAsync(config.Identifier);

		// Assert	
		storedFlag.ShouldNotBeNull();
		storedFlag.ActiveEvaluationModes.Modes.ShouldContain(EvaluationMode.On);
	}
}

public class IsEnabledAsync_WithTargetedFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_UserTargeted_ThenReturnsTrue()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("client-targeted")
					.WithEvaluationModes(EvaluationMode.UserTargeted)
					.WithTargetingRules([TargetingRuleFactory.CreateTargetingRule(
						"region",
						TargetingOperator.Equals,
						["us-west"],
						"regional-feature")])
					.ForFeatureFlag(defaultMode: EvaluationMode.Off)
					.Build();

		await fixture.SaveAsync(config, "client-targeted-flag", "created by integration tests");

		// Act
		var result = await fixture.Client.IsEnabledAsync(flag, 
			userId: "user123", 
			attributes: new Dictionary<string, object> { { "region", "us-west" } });

		// Assert
		result.ShouldBeTrue();
	}
}

public class GetVariationAsync_WithStringVariation(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task ThenReturnsVariationValue()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("client-targeted")
			.WithEvaluationModes(EvaluationMode.TargetingRules)
			.WithVariations(new Variations
				{
					Values = new Dictionary<string, object>
					{
						{ "premium-config", "premium-dashboard" }
					}
				})
			.WithTargetingRules([TargetingRuleFactory.CreateTargetingRule(
								"subscription_level",
								TargetingOperator.In,
								["premium-user"],
								"premium-config")])
			.ForFeatureFlag(defaultMode: EvaluationMode.Off)
			.Build();

		await fixture.SaveAsync(config, "client-targeted-flag", "created by integration tests");

		// Act
		var result = await fixture.Client.GetVariationAsync(flag, "default",
			userId: "user123",
			attributes: new Dictionary<string, object> { { "subscription_level", "premium-user" } });

		// Assert
		result.ShouldBe("premium-dashboard");
	}
}

public class GetVariationAsync_WithDisabledFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("client-disabled")
									.WithEvaluationModes(EvaluationMode.Off)
									.ForFeatureFlag(defaultMode: EvaluationMode.Off)
									.Build();

		await fixture.SaveAsync(config, "client-disabled-flag", "created by integration tests");

		// Act
		var result = await fixture.Client.GetVariationAsync(flag, "fallback-value", userId: "user123");

		// Assert
		result.ShouldBe("fallback-value");
	}
}

public class EvaluateAsync_WithTimeWindowFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_WithinWindow_ThenReturnsEnabledResult()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("client-window")
							.WithEvaluationModes(EvaluationMode.TimeWindow)
							.ForFeatureFlag(defaultMode: EvaluationMode.Off)
							.Build();

		await fixture.SaveAsync(config, "client-window", "created by integration tests");

		// Act
		var result = await fixture.Client.EvaluateAsync(flag, tenantId: "tenant123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag operational window is always open.");
	}
}