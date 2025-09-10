using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class UserRolloutEvaluatorTests
{
	private readonly UserRolloutEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_WhenUserExplicitlyManaged_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			UserAccess = new FlagUserAccessControl(allowedUsers: ["user123"])
		};
		var context = new EvaluationContext(userId: "user123");

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_WhenPercentageRolloutWithRestrictions_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			UserAccess = new FlagUserAccessControl(rolloutPercentage: 50)
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
		var context = new EvaluationContext(userId: "user123");

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_WhenNoUserManagementOrRestrictions_ReturnsFalse()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			UserAccess = FlagUserAccessControl.Unrestricted,
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		var context = new EvaluationContext(userId: "user123");

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeFalse();
	}

	[Fact]
	public async Task ProcessEvaluation_WhenNoUserId_ThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = new FlagUserAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("User ID is required for percentage rollout evaluation.");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenUserExplicitlyAllowed_ReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = new FlagUserAccessControl(allowedUsers: ["user123"]),
			Variations = new FlagVariations { DefaultVariation = "disabled" }
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenUserExplicitlyBlocked_ReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = new FlagUserAccessControl(
				blockedUsers: ["user123"],
				rolloutPercentage: 100), // Blocked overrides rollout
			Variations = new FlagVariations { DefaultVariation = "blocked" }
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
		result.Reason.ShouldBe("User explicitly blocked");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenZeroPercentRollout_ReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = new FlagUserAccessControl(rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = "restricted" }
		};
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("restricted");
		result.Reason.ShouldBe("Access restricted to all users");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenHundredPercentRollout_ReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = new FlagUserAccessControl(rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "restricted" }
		};
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Access unrestricted to all users");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenPartialRollout_IsConsistent()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = new FlagUserAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "not-in-rollout" }
		};
		var context = new EvaluationContext(userId: "consistent-user");

		// Act - Multiple evaluations should be consistent
		var result1 = await _evaluator.ProcessEvaluation(flag, context);
		var result2 = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		result1.IsEnabled.ShouldBe(result2.IsEnabled);
		result1.Variation.ShouldBe(result2.Variation);
		result1.Reason.ShouldBe(result2.Reason);

		// Verify reason format is correct
		if (result1.IsEnabled)
		{
			result1.Reason.ShouldMatch(@"User in rollout: \d+% < 50%");
		}
		else
		{
			result1.Reason.ShouldMatch(@"User not in rollout: \d+% >= 50%");
		}
	}

	[Fact]
	public async Task ProcessEvaluation_BetaRolloutScenario_WorksCorrectly()
	{
		// Arrange - Beta testers get early access, plus gradual rollout
		var flag = new FeatureFlag
		{
			Key = "beta-feature",
			UserAccess = new FlagUserAccessControl(
				allowedUsers: ["beta-tester"],
				rolloutPercentage: 15),
			Variations = new FlagVariations { DefaultVariation = "stable-version" }
		};

		// Act
		var betaResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "beta-tester"));
		var regularResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "regular-user"));

		// Assert
		betaResult.IsEnabled.ShouldBeTrue();
		betaResult.Variation.ShouldBe("on");
		betaResult.Reason.ShouldBe("User explicitly allowed");

		regularResult.IsEnabled.ShouldBeOneOf(true, false);
		if (regularResult.IsEnabled)
		{
			regularResult.Reason.ShouldMatch(@"User in rollout: \d+% < 15%");
		}
		else
		{
			regularResult.Reason.ShouldMatch(@"User not in rollout: \d+% >= 15%");
		}
	}
}