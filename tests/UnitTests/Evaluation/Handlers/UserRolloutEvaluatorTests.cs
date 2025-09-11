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
			UserAccessControl = new AccessControl(allowed: ["user123"])
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
			ActiveEvaluationModes = new EvaluationModes(),
			UserAccessControl = new AccessControl(rolloutPercentage: 50)
		};
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.UserRolloutPercentage);
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
			UserAccessControl = AccessControl.Unrestricted,
			ActiveEvaluationModes = new EvaluationModes()
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
			UserAccessControl = new AccessControl(rolloutPercentage: 50),
			Variations = new Variations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
	}

	[Fact]
	public async Task ProcessEvaluation_WhenUserExplicitlyAllowed_ReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccessControl = new AccessControl(allowed: ["user123"]),
		};

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenUserExplicitlyBlocked_ReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccessControl = new AccessControl(
				blocked: ["user123"],
				rolloutPercentage: 100), // Blocked overrides rollout
			Variations = new Variations { DefaultVariation = "blocked" }
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenZeroPercentRollout_ReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccessControl = new AccessControl(rolloutPercentage: 0),
			Variations = new Variations { DefaultVariation = "restricted" }
		};
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("restricted");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenHundredPercentRollout_ReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccessControl = new AccessControl(rolloutPercentage: 100),
		};
		flag.Variations.DefaultVariation = "restricted";
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenPartialRollout_IsConsistent()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccessControl = new AccessControl(rolloutPercentage: 50),
			Variations = new Variations { DefaultVariation = "not-in-rollout" }
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
			result1.Reason.ShouldContain(@"< 50%");
		}
		else
		{
			result1.Reason.ShouldContain(@">= 50%");
		}
	}

	[Fact]
	public async Task ProcessEvaluation_BetaRolloutScenario_WorksCorrectly()
	{
		// Arrange - Beta testers get early access, plus gradual rollout
		var flag = new FeatureFlag
		{
			Key = "beta-feature",
			UserAccessControl = new AccessControl(
				allowed: ["beta-tester"],
				rolloutPercentage: 15),
		};
		flag.Variations.DefaultVariation = "stable-version";
		// Act
		var betaResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "beta-tester"));
		var regularResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "regular-user"));

		// Assert
		betaResult.IsEnabled.ShouldBeTrue();
		betaResult.Variation.ShouldBe("on");

		regularResult.IsEnabled.ShouldBeOneOf(true, false);
		if (regularResult.IsEnabled)
		{
			regularResult.Reason.ShouldContain(@"< 15%");
		}
		else
		{
			regularResult.Reason.ShouldContain(@">= 15%");
		}
	}
	
	[Fact]
	public async Task ProcessEvaluation_SimpleOnOffFlag_ReturnsOnVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "simple-flag",
			UserAccessControl = new AccessControl(allowed: ["user123"]),
			Variations = new Variations 
			{ 
				Values = new Dictionary<string, object> { { "on", true }, { "off", false } },
				DefaultVariation = "off" 
			}
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task ProcessEvaluation_MultipleVariations_ReturnsConsistentVariationSelection()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "checkout-version",
			UserAccessControl = new AccessControl(allowed: ["user123"]),
			Variations = new Variations 
			{ 
				Values = new Dictionary<string, object> 
				{
					{ "v1", "version1" },
					{ "v2", "version2" }, 
					{ "v3", "version3" }
				},
				DefaultVariation = "v1" 
			}
		};
		var context = new EvaluationContext(userId: "user123");

		// Act - Multiple calls should return same variation
		var result1 = await _evaluator.ProcessEvaluation(flag, context);
		var result2 = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		result1.IsEnabled.ShouldBeTrue();
		result2.IsEnabled.ShouldBeTrue();
		result1.Variation.ShouldBe(result2.Variation);
		result1.Variation.ShouldBeOneOf("v1", "v2", "v3");
	}
}