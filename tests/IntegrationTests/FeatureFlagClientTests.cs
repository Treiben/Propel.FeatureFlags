using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.Client;

public class IsEnabledAsync_WithEnabledFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_CreateItDuringEvaluation()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("client-enabled", EvaluationMode.Enabled, EvaluationMode.Enabled);

		// Act
		var result = await fixture.Client.IsEnabledAsync(appFlag, userId: "user123");

		// Assert
		result.ShouldBeTrue();

		// Verify the flag was created in the repository

		//A Arrange
		var storedFlag = await fixture.EvaluationRepository.GetAsync(flag.Key);

		// Assert	
		storedFlag.ShouldNotBeNull();
		storedFlag.ActiveEvaluationModes.Modes.ShouldContain(EvaluationMode.Enabled);
	}
}

public class IsEnabledAsync_WithTargetedFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_UserTargeted_ThenReturnsTrue()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("client-targeted", EvaluationMode.UserTargeted);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule(
				"region",
				TargetingOperator.Equals,
				["us-west"],
				"regional-feature"
			)
		];
		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.IsEnabledAsync(appFlag, 
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
		var (flag, appFlag) = TestHelpers.SetupTestCases("client-variation", EvaluationMode.TargetingRules);

		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule(
								"subscription_level",
								TargetingOperator.In,
								["premium-user"],
								"premium-config"
							)
		];
		flag.Variations = new Variations
		{
			Values = new Dictionary<string, object>
							{
								{ "premium-config", "premium-dashboard" }
							}
		};
		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.GetVariationAsync(appFlag, "default",
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
		var (flag, appFlag) = TestHelpers.SetupTestCases("client-disabled", EvaluationMode.Disabled);

		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.GetVariationAsync(appFlag, "fallback-value", userId: "user123");

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
		var (flag, appFlag) = TestHelpers.SetupTestCases("client-window", EvaluationMode.TimeWindow);
		flag.OperationalWindow = OperationalWindow.AlwaysOpen;

		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.EvaluateAsync(appFlag, tenantId: "tenant123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag operational window is always open.");
	}
}