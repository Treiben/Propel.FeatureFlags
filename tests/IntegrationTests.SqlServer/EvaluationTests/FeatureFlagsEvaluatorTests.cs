using FeatureFlags.IntegrationTests.SqlServer.EvaluationTests;
using FeatureFlags.IntegrationTests.SqlServer.Support;
using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.EvaluationTests.Evaluator;

public class Evaluate_WithEnabledFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("enabled-test")
		.WithEvaluationModes(EvaluationMode.On)
		.ForFeatureFlag(defaultMode: EvaluationMode.On)
		.Build();

		await fixture.FeatureFlagRepository.CreateAsync(config.Identifier, flag.OnOffMode, flag.Name, flag.Description);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
	}
}

public class Evaluate_WithUserTargetedFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{

	[Fact]
	public async Task If_UserNotTargeted_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("targeted-flag")
									.WithEvaluationModes(EvaluationMode.UserTargeted)
									.WithTargetingRules([TargetingRuleFactory.CreateTargetingRule(
											"userId",
											TargetingOperator.Equals,
											["target-user"],
											"premium-features"
										)])
									.ForFeatureFlag(defaultMode: EvaluationMode.Off)
									.Build();

		await fixture.SaveAsync(config, "targeted-flag", "created by integration tests");

		var context = new EvaluationContext(userId: "current-user");

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}
}

public class Evaluate_WithUserRolloutFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_UserInAllowedList_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("rollout-flag")
							.WithEvaluationModes(EvaluationMode.UserRolloutPercentage)
							.WithUserAccessControl(new AccessControl(
								allowed: ["allowed-user"],
								rolloutPercentage: 0))
							.ForFeatureFlag(defaultMode: EvaluationMode.Off)
							.Build();

		await fixture.SaveAsync(config, "rollout-flag", "created by integration tests");

		var context = new EvaluationContext(userId: "allowed-user");

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task If_UserInBlockedList_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("rollout-flag")
					.WithEvaluationModes(EvaluationMode.UserRolloutPercentage)
					.WithUserAccessControl(new AccessControl(
						blocked: ["blocked-user"],
						rolloutPercentage: 100))
					.ForFeatureFlag(defaultMode: EvaluationMode.Off)
					.Build();

		await fixture.SaveAsync(config, "rollout-flag", "created by integration tests");

		var context = new EvaluationContext(userId: "blocked-user");

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}
}

public class Evaluate_WithTimeWindowFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_WithinTimeWindow_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("window-flag")
				.WithEvaluationModes(EvaluationMode.TimeWindow)
				.WithOperationalWindow(new UtcTimeWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(17)))
				.ForFeatureFlag(defaultMode: EvaluationMode.Off)
				.Build();

		await fixture.SaveAsync(config, "window-flag", "created by integration tests");

		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: new UtcDateTime(evaluationTime));

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task If_OutsideTimeWindow_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("window-flag")
					.WithEvaluationModes(EvaluationMode.TimeWindow)
					.WithOperationalWindow(new UtcTimeWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(17)))
					.ForFeatureFlag(defaultMode: EvaluationMode.Off)
					.Build();

		await fixture.SaveAsync(config, "window-flag", "created by integration tests");

		var evaluationTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: new UtcDateTime(evaluationTime));

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}
}

public class GetVariation_WithComplexVariations(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_VariationIsString_ThenReturnsString()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("string-flag")
				.WithEvaluationModes(EvaluationMode.UserTargeted)
				.WithTargetingRules([TargetingRuleFactory.CreateTargetingRule(
								attribute: "user-type",
								op: TargetingOperator.In,
								values: ["dev-user", "test-user"],
								variation: "string-variant"
							)])
				.WithVariations(new Variations { 
					Values = new Dictionary<string, object> 
					{ 
						{ "string-variant", "Hello World" } 
					} 
				})
				.ForFeatureFlag(defaultMode: EvaluationMode.Off)
				.Build();

		await fixture.SaveAsync(config, "string-flag", "created by integration tests");

		var context = new EvaluationContext(attributes: new Dictionary<string, object> { { "user-type", "test-user" } },
			userId: "dev-user");

		// Act
		var result = await fixture.Evaluator.GetVariation(flag, "default", context);

		// Assert
		result.ShouldBe("Hello World");
	}

	[Fact]
	public async Task If_VariationIsInteger_ThenReturnsInteger()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("int-flag")
				.WithEvaluationModes(EvaluationMode.UserTargeted)
				.WithTargetingRules([TargetingRuleFactory.CreateTargetingRule(
										"userId",
										TargetingOperator.Equals,
										["test-user"],
										"int-variant"
									)])
				.WithVariations(new Variations
				{
					Values = new Dictionary<string, object>
						{
							{ "int-variant", 42 }
						}
				})
		.ForFeatureFlag(defaultMode: EvaluationMode.Off)
		.Build();

		await fixture.SaveAsync(config, "integer-flag", "created by integration tests");

		var context = new EvaluationContext(
			userId: "test-user",
			attributes: new Dictionary<string, object> { { "userId", "test-user" } }
		);

		// Act
		var result = await fixture.Evaluator.GetVariation(flag, 0, context);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task If_FlagDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();

		var (config, flag) = new FlagConfigurationBuilder("disabled-variation")
				.WithEvaluationModes(EvaluationMode.Off)
				.ForFeatureFlag(defaultMode: EvaluationMode.Off)
				.Build();

		await fixture.SaveAsync(config, "disabled-variation", "created by integration tests");

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await fixture.Evaluator.GetVariation(flag, "default-value", context);

		// Assert
		result.ShouldBe("default-value");
	}
}