using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.Core.Client;

public class IsEnabledAsync_WithEnabledFlag(ClientTestsFixture fixture) : IClassFixture<ClientTestsFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_CreateItDuringEvaluation()
	{
		// Arrange
		await fixture.ClearAllData();
		var (_, appFlag) = TestHelpers.SetupTestCases("client-enabled", EvaluationMode.Enabled, EvaluationMode.Enabled);

		// Act
		var result = await fixture.Client.IsEnabledAsync(appFlag, userId: "user123");

		// Assert
		result.ShouldBeTrue();

		// Verify the flag was created in the repository

		//A Arrange
		var storedFlag = await fixture.Repository.GetAllAsync();

		// Act
		var outflag = storedFlag.FirstOrDefault(f => f.Key == "client-enabled");

		// Assert	
		outflag.ShouldNotBeNull();
		outflag.ActiveEvaluationModes.Modes.ShouldContain(EvaluationMode.Enabled);
	}
}

public class IsEnabledAsync_WithTargetedFlag(ClientTestsFixture fixture) : IClassFixture<ClientTestsFixture>
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
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.IsEnabledAsync(appFlag, 
			userId: "user123", 
			attributes: new Dictionary<string, object> { { "region", "us-west" } });

		// Assert
		result.ShouldBeTrue();
	}
}

public class GetVariationAsync_WithStringVariation(ClientTestsFixture fixture) : IClassFixture<ClientTestsFixture>
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
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.GetVariationAsync(appFlag, "default",
			userId: "user123",
			attributes: new Dictionary<string, object> { { "subscription_level", "premium-user" } });

		// Assert
		result.ShouldBe("premium-dashboard");
	}
}

public class GetVariationAsync_WithDisabledFlag(ClientTestsFixture fixture) : IClassFixture<ClientTestsFixture>
{
	[Fact]
	public async Task ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("client-disabled", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.GetVariationAsync(appFlag, "fallback-value", userId: "user123");

		// Assert
		result.ShouldBe("fallback-value");
	}
}

public class EvaluateAsync_WithTimeWindowFlag(ClientTestsFixture fixture) : IClassFixture<ClientTestsFixture>
{
	[Fact]
	public async Task If_WithinWindow_ThenReturnsEnabledResult()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("client-window", EvaluationMode.TimeWindow);
		flag.OperationalWindow = OperationalWindow.AlwaysOpen;
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.EvaluateAsync(appFlag, tenantId: "tenant123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag operational window is always open.");
	}
}