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
		var flag = TestHelpers.CreateTestFlag("enabled-test", EvaluationMode.Enabled);
		var created = await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");
		var applicationFlag = TestHelpers.CreateApplicationFlag(flag.Key, EvaluationMode.Enabled);

		// Act
		var result = await fixture.Evaluator.Evaluate(applicationFlag, context);

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
		var flag = TestHelpers.CreateTestFlag("targeted-flag", EvaluationMode.UserTargeted);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule(
				"userId",
				TargetingOperator.Equals,
				["target-user"],
				"premium-features"
			),
		];
		await fixture.Repository.CreateAsync(flag);
		var applicationFlag = TestHelpers.CreateApplicationFlag("targeted-flag", EvaluationMode.Enabled);

		var context = new EvaluationContext(userId: "current-user");

		// Act
		var result = await fixture.Evaluator.Evaluate(applicationFlag, context);

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
		var flag = TestHelpers.CreateTestFlag("rollout-flag", EvaluationMode.UserRolloutPercentage);
		flag.UserAccessControl = new AccessControl(
			allowed: ["allowed-user"],
			rolloutPercentage: 0);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "allowed-user");
		var applicationFlag = TestHelpers.CreateApplicationFlag("rollout-flag", EvaluationMode.Enabled);

		// Act
		var result = await fixture.Evaluator.Evaluate(applicationFlag, context);

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
		var flag = TestHelpers.CreateTestFlag("rollout-flag", EvaluationMode.UserRolloutPercentage);
		flag.UserAccessControl = new AccessControl(
			blocked: ["blocked-user"],
			rolloutPercentage: 100);
		await fixture.Repository.CreateAsync(flag);
		var applicationFlag = TestHelpers.CreateApplicationFlag("rollout-flag", EvaluationMode.Enabled);

		var context = new EvaluationContext(userId: "blocked-user");

		// Act
		var result = await fixture.Evaluator.Evaluate(applicationFlag, context);

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
		var flag = TestHelpers.CreateTestFlag("window-flag", EvaluationMode.TimeWindow);
		flag.OperationalWindow = new OperationalWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));
		await fixture.Repository.CreateAsync(flag);
		var applicationFlag = TestHelpers.CreateApplicationFlag("window-flag", EvaluationMode.Enabled);

		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await fixture.Evaluator.Evaluate(applicationFlag, context);

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
		var flag = TestHelpers.CreateTestFlag("window-flag", EvaluationMode.TimeWindow);
		flag.OperationalWindow = new OperationalWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));
		await fixture.Repository.CreateAsync(flag);
		var applicationFlag = TestHelpers.CreateApplicationFlag("window-flag", EvaluationMode.Enabled);

		var evaluationTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await fixture.Evaluator.Evaluate(applicationFlag, context);

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
		var flag = TestHelpers.CreateTestFlag("string-flag", EvaluationMode.UserTargeted);
		
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule(
							attribute: "user-type",
							op: TargetingOperator.In,
							values: new List<string> { "dev-user", "test-user" },
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

		var applicationFlag = TestHelpers.CreateApplicationFlag(flag.Key, EvaluationMode.Enabled);

		var context = new EvaluationContext(attributes: new Dictionary<string, object> { { "user-type", "test-user" } },
			userId: "dev-user");

		// Act
		var result = await fixture.Evaluator.GetVariation(applicationFlag, "default", context);

		// Assert
		result.ShouldBe("Hello World");
	}

	[Fact]
	public async Task If_VariationIsInteger_ThenReturnsInteger()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("int-flag", EvaluationMode.UserTargeted);
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
		var applicationFlag = TestHelpers.CreateApplicationFlag("int-flag", EvaluationMode.Enabled);

		var context = new EvaluationContext(
			userId: "test-user",
			attributes: new Dictionary<string, object> { { "userId", "test-user" } }
		);

		// Act
		var result = await fixture.Evaluator.GetVariation(applicationFlag, 0, context);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task If_FlagDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("disabled-variation", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);
		var applicationFlag = TestHelpers.CreateApplicationFlag("disabled-variation", EvaluationMode.Enabled);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await fixture.Evaluator.GetVariation(applicationFlag, "default-value", context);

		// Assert
		result.ShouldBe("default-value");
	}
}