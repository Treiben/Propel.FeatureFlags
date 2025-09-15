using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.Core.Client;

public class IsEnabledAsync_WithEnabledFlag(ClientTestsFixture fixture) : IClassFixture<ClientTestsFixture>
{
	[Fact]
	public async Task ThenReturnsTrue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("client-enabled", EvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Client.IsEnabledAsync("client-enabled", userId: "user123");

		// Assert
		result.ShouldBeTrue();
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

		// Act
		var result = await fixture.Client.IsEnabledAsync("client-targeted", 
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

		// Act
		var result = await fixture.Client.GetVariationAsync("client-variation", "default",
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

		// Act
		var result = await fixture.Client.GetVariationAsync("client-disabled", "fallback-value", userId: "user123");

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

		// Act
		var result = await fixture.Client.EvaluateAsync("client-window", tenantId: "tenant123");

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag operational window is always open.");
	}
}