using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.Core.Evaluator;

public class Evaluate_WithEnabledFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("enabled-test", EvaluationMode.Enabled);
		_ = await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate(appFlag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}
}

public class Evaluate_WithUserTargetedFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{

	[Fact]
	public async Task If_UserNotTargeted_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("targeted-flag", EvaluationMode.UserTargeted);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule(
				"userId",
				TargetingOperator.Equals,
				["target-user"],
				"premium-features"
			),
		];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "current-user");

		// Act
		var result = await fixture.Evaluator.Evaluate(appFlag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
	}
}

public class Evaluate_WithUserRolloutFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task If_UserInAllowedList_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("rollout-flag", EvaluationMode.UserRolloutPercentage);
		flag.UserAccessControl = new AccessControl(
			allowed: ["allowed-user"],
			rolloutPercentage: 0);

		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "allowed-user");

		// Act
		var result = await fixture.Evaluator.Evaluate(appFlag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task If_UserInBlockedList_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("rollout-flag", EvaluationMode.UserRolloutPercentage);
		flag.UserAccessControl = new AccessControl(
			blocked: ["blocked-user"],
			rolloutPercentage: 100);

		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "blocked-user");

		// Act
		var result = await fixture.Evaluator.Evaluate(appFlag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
	}
}

public class Evaluate_WithTimeWindowFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task If_WithinTimeWindow_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("window-flag", EvaluationMode.TimeWindow);
		flag.OperationalWindow = new OperationalWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));

		await fixture.Repository.CreateAsync(flag);

		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await fixture.Evaluator.Evaluate(appFlag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task If_OutsideTimeWindow_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("window-flag", EvaluationMode.TimeWindow);
		flag.OperationalWindow = new OperationalWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));

		await fixture.Repository.CreateAsync(flag);

		var evaluationTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await fixture.Evaluator.Evaluate(appFlag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
	}
}

public class GetVariation_WithComplexVariations(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task If_VariationIsString_ThenReturnsString()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("string-flag", EvaluationMode.UserTargeted);
		
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule(
							attribute: "user-type",
							op: TargetingOperator.In,
							values: ["dev-user", "test-user"],
							variation: "string-variant"
						)
		];
		flag.Variations = new Variations
		{
			Values = new Dictionary<string, object>
						{
							{ "string-variant", "Hello World" }
						}
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(attributes: new Dictionary<string, object> { { "user-type", "test-user" } },
			userId: "dev-user");

		// Act
		var result = await fixture.Evaluator.GetVariation(appFlag, "default", context);

		// Assert
		result.ShouldBe("Hello World");
	}

	[Fact]
	public async Task If_VariationIsInteger_ThenReturnsInteger()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("int-flag", EvaluationMode.UserTargeted);
		flag.TargetingRules = [
		TargetingRuleFactory.CreateTargetingRule(
								"userId",
								TargetingOperator.Equals,
								["test-user"],
								"int-variant"
								)
		];
		flag.Variations = new Variations
		{
			Values = new Dictionary<string, object>
						{
							{ "int-variant", 42 }
						}
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(
			userId: "test-user",
			attributes: new Dictionary<string, object> { { "userId", "test-user" } }
		);

		// Act
		var result = await fixture.Evaluator.GetVariation(appFlag, 0, context);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task If_FlagDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, appFlag) = TestHelpers.SetupTestCases("disabled-variation", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await fixture.Evaluator.GetVariation(appFlag, "default-value", context);

		// Assert
		result.ShouldBe("default-value");
	}
}