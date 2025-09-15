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
		var flag = TestHelpers.CreateApplicationFlag("client-enabled", EvaluationMode.Enabled);

		// Act
		var result = await fixture.Client.IsEnabledAsync(flag, userId: "user123");

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
		var flag = TestHelpers.CreateTestFlag("client-targeted", EvaluationMode.UserTargeted);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule(
				"region",
				TargetingOperator.Equals,
				["us-west"],
				"regional-feature"
			)
		];
		await fixture.Repository.CreateAsync(flag);

		var applicationFlag = TestHelpers.CreateApplicationFlag("client-targeted", EvaluationMode.Enabled);

		// Act
		var result = await fixture.Client.IsEnabledAsync(applicationFlag, 
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
		var flag = TestHelpers.CreateTestFlag("client-variation", EvaluationMode.TargetingRules);

		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule(
								"userId",
								TargetingOperator.Equals,
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

		var applicationFlag = TestHelpers.CreateApplicationFlag("client-variation", EvaluationMode.Disabled);
		// Act

		var result = await fixture.Client.GetVariationAsync(applicationFlag, "default",
			attributes: new Dictionary<string, object> { { "userId", "premium-user" } });

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
		var flag = TestHelpers.CreateTestFlag("client-disabled", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var applicationFlag = TestHelpers.CreateApplicationFlag("client-disabled", EvaluationMode.Disabled);

		// Act
		var result = await fixture.Client.GetVariationAsync(applicationFlag, "fallback-value", userId: "user123");

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
		var flag = TestHelpers.CreateTestFlag("client-window", EvaluationMode.TimeWindow);
		flag.OperationalWindow = OperationalWindow.AlwaysOpen;
		await fixture.Repository.CreateAsync(flag);

		var applicationFlag = TestHelpers.CreateApplicationFlag("client-window", EvaluationMode.Disabled);
		// Act
		var result = await fixture.Client.EvaluateAsync(applicationFlag, tenantId: "tenant123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag operational window is always open.");
	}
}