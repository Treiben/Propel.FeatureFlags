using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;

namespace FeatureFlags.IntegrationTests.Core.Evaluator;

public class Evaluate_WithEnabledFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("enabled-test", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("enabled-test", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Feature flag 'enabled-test' is explicitly enabled");
	}

	[Fact]
	public async Task If_FlagInCache_ThenUsesCache()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-flag", FlagEvaluationMode.Enabled);
		await fixture.Cache.SetAsync("cached-flag", flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("cached-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}
}

public class Evaluate_WithDisabledFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("disabled-test", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("disabled-test", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Feature flag 'disabled-test' is explicitly disabled");
	}
}

public class Evaluate_WithUserTargetedFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task If_UserIsTargeted_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("targeted-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["target-user"],
				Variation = "premium-features"
			}
		];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "target-user");

		// Act
		var result = await fixture.Evaluator.Evaluate("targeted-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("premium-features");
		result.Reason.ShouldBe("Targeting rule matched: userId Equals target-user");
	}

	[Fact]
	public async Task If_UserNotTargeted_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("targeted-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["other-user"],
				Variation = "premium-features"
			}
		];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "current-user");

		// Act
		var result = await fixture.Evaluator.Evaluate("targeted-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class Evaluate_WithUserRolloutFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task If_UserInAllowedList_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("rollout-flag", FlagEvaluationMode.UserRolloutPercentage);
		flag.UserAccess = new FlagUserAccessControl(
			allowedUsers: ["allowed-user"],
			rolloutPercentage: 0);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "allowed-user");

		// Act
		var result = await fixture.Evaluator.Evaluate("rollout-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public async Task If_UserInBlockedList_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("rollout-flag", FlagEvaluationMode.UserRolloutPercentage);
		flag.UserAccess = new FlagUserAccessControl(
			blockedUsers: ["blocked-user"],
			rolloutPercentage: 100);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "blocked-user");

		// Act
		var result = await fixture.Evaluator.Evaluate("rollout-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("User explicitly blocked");
	}
}

public class Evaluate_WithTimeWindowFlag(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task If_WithinTimeWindow_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("window-flag", FlagEvaluationMode.TimeWindow);
		flag.OperationalWindow = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));
		await fixture.Repository.CreateAsync(flag);

		var evaluationTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await fixture.Evaluator.Evaluate("window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_OutsideTimeWindow_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("window-flag", FlagEvaluationMode.TimeWindow);
		flag.OperationalWindow = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17));
		await fixture.Repository.CreateAsync(flag);

		var evaluationTime = new DateTime(2024, 1, 15, 20, 0, 0, DateTimeKind.Utc);
		var context = new EvaluationContext(evaluationTime: evaluationTime);

		// Act
		var result = await fixture.Evaluator.Evaluate("window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Outside time window");
	}
}

public class GetVariation_WithComplexVariations(EvaluatorTestsFixture fixture) : IClassFixture<EvaluatorTestsFixture>
{
	[Fact]
	public async Task If_VariationIsString_ThenReturnsString()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("string-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["test-user"],
				Variation = "string-variant"
			}
		];
		flag.Variations = new FlagVariations
		{
			Values = new Dictionary<string, object>
			{
				{ "string-variant", "Hello World" }
			}
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await fixture.Evaluator.GetVariation("string-flag", "default", context);

		// Assert
		result.ShouldBe("Hello World");
	}

	[Fact]
	public async Task If_VariationIsInteger_ThenReturnsInteger()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("int-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules = [
			new TargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = ["test-user"],
				Variation = "int-variant"
			}
		];
		flag.Variations = new FlagVariations
		{
			Values = new Dictionary<string, object>
			{
				{ "int-variant", 42 }
			}
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await fixture.Evaluator.GetVariation("int-flag", 0, context);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task If_FlagDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("disabled-variation", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await fixture.Evaluator.GetVariation("disabled-variation", "default-value", context);

		// Assert
		result.ShouldBe("default-value");
	}
}