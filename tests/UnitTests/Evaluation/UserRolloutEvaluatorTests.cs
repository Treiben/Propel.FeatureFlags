using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace FeatureFlags.UnitTests.Evaluation;

public class UserRolloutEvaluatorTests
{
	private readonly UserRolloutEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_WhenUserExplicitlyManaged_ReturnsTrue()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var userAccessControl = new AccessControl(allowed: ["user123"]);
		var flagConfig = new EvaluationOptions(key: identifier.Key, userAccessControl: userAccessControl);
		var context = new EvaluationContext(userId: "user123");

		// Act & Assert
		_evaluator.CanProcess(flagConfig, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_WhenPercentageRolloutWithRestrictions_ReturnsTrue()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var activeEvaluationModes = new ModeSet([EvaluationMode.UserRolloutPercentage]);
		var userAccessControl = new AccessControl(rolloutPercentage: 50);
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			modeSet: activeEvaluationModes,
			userAccessControl: userAccessControl);
		var context = new EvaluationContext(userId: "user123");

		// Act & Assert
		_evaluator.CanProcess(flagConfig, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_WhenNoUserManagementOrRestrictions_ReturnsFalse()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var criteria = new EvaluationOptions(identifier.Key);
		var context = new EvaluationContext(userId: "user123");

		// Act & Assert
		_evaluator.CanProcess(criteria, context).ShouldBeFalse();
	}

	[Fact]
	public async Task ProcessEvaluation_WhenNoUserId_ThrowsEvalationArgumentException()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(rolloutPercentage: 50);
		var Variations = new Variations { DefaultVariation = "off" };
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl,
			variations: Variations);
		var context = new EvaluationContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<EvaluationOptionsArgumentException>(
			async () => await _evaluator.Evaluate(flagConfig, context));
	}

	[Fact]
	public async Task ProcessEvaluation_WhenUserExplicitlyAllowed_ReturnsEnabled()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(allowed: ["user123"]);
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenUserExplicitlyBlocked_ReturnsDisabled()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(
				blocked: ["user123"],
				rolloutPercentage: 100); // Blocked overrides rollout
		var Variations = new Variations { DefaultVariation = "blocked" };
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl,
			variations: Variations);
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenZeroPercentRollout_ReturnsDisabled()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(rolloutPercentage: 0);
		var Variations = new Variations { DefaultVariation = "restricted" };
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl,
			variations: Variations);
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("restricted");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenHundredPercentRollout_ReturnsEnabled()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(rolloutPercentage: 100);
		var Variations = new Variations
		{
			Values = new Dictionary<string, object>
			{
				{ "enabled", true },
				{ "restricted", false }
			},
			DefaultVariation = "restricted"
		};
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl,
			variations: Variations);
		flagConfig.Variations.DefaultVariation = "restricted";
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("enabled");
	}

	[Fact]
	public async Task ProcessEvaluation_WhenPartialRollout_IsConsistent()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(rolloutPercentage: 50);
		var Variations = new Variations { DefaultVariation = "not-in-rollout" };
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl,
			variations: Variations);
		var context = new EvaluationContext(userId: "consistent-user");

		// Act - Multiple evaluations should be consistent
		var result1 = await _evaluator.Evaluate(flagConfig, context);
		var result2 = await _evaluator.Evaluate(flagConfig, context);

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
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(
				allowed: ["beta-tester"],
				rolloutPercentage: 15);
		var Variations = new Variations
		{
			Values = new Dictionary<string, object> {
					{ "stable-version", true },
					{ "old-version", false }
				},
			DefaultVariation = "old-version"
		};
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl,
			variations: Variations);

		// Act
		var betaResult = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(userId: "beta-tester"));
		var regularResult = await _evaluator.Evaluate(flagConfig,
			new EvaluationContext(userId: "regular-user"));

		// Assert
		betaResult.IsEnabled.ShouldBeTrue();
		betaResult.Variation.ShouldBe("stable-version");

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
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(allowed: ["user123"]);
		var Variations = new Variations
		{
			Values = new Dictionary<string, object> { { "on", true }, { "off", false } },
			DefaultVariation = "off"
		};
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl,
			variations: Variations);
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task ProcessEvaluation_MultipleVariations_ReturnsConsistentVariationSelection()
	{
		// Arrange
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var UserAccessControl = new AccessControl(allowed: ["user123"]);
		var Variations = new Variations
		{
			Values = new Dictionary<string, object>
				{
					{ "v1", "version1" },
					{ "v2", "version2" },
					{ "v3", "version3" }
				},
			DefaultVariation = "v1"
		};
		var flagConfig = new EvaluationOptions(
			key: identifier.Key,
			userAccessControl: UserAccessControl,
			variations: Variations);
		var context = new EvaluationContext(userId: "user123");

		// Act - Multiple calls should return same variation
		var result1 = await _evaluator.Evaluate(flagConfig, context);
		var result2 = await _evaluator.Evaluate(flagConfig, context);

		// Assert
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		result1.IsEnabled.ShouldBeTrue();
		result2.IsEnabled.ShouldBeTrue();
		result1.Variation.ShouldBe(result2.Variation);
		result1.Variation.ShouldBeOneOf("v1", "v2", "v3");
	}
}