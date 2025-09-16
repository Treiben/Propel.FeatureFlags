using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Services.Evaluation;

namespace FeatureFlags.UnitTests.Evaluation;

public class TenantRolloutEvaluator_CanProcess
{
	private readonly TenantRolloutEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_TenantExplicitlyManaged_ReturnsTrue()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			TenantAccessControl = new AccessControl(allowed: ["tenant123"])
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act & Assert
		_evaluator.CanProcess(criteria, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_TenantRolloutModeWithRestrictions_ReturnsTrue()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.TenantRolloutPercentage]),
			TenantAccessControl = new AccessControl(rolloutPercentage: 95)
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act & Assert
		_evaluator.CanProcess(criteria, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_TenantRolloutModeButNoRestrictions_ReturnsTrue()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.TenantRolloutPercentage]),
			TenantAccessControl = AccessControl.Unrestricted // No restrictions
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act & Assert
		_evaluator.CanProcess(criteria, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_NoTenantIdAndNoTenantMode_ButRestrictedAccess_ShouldReturnTrue()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			TenantAccessControl = new AccessControl(allowed: ["tenant123"])
		};
		var context = new EvaluationContext(userId: "user123"); // No tenant ID

		// Act & Assert
		_evaluator.CanProcess(criteria, context).ShouldBeTrue();
	}
}

public class TenantRolloutEvaluator_ProcessEvaluation
{
	private readonly TenantRolloutEvaluator _evaluator = new();

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task ProcessEvaluation_InvalidTenantId_ThrowsInvalidOperationException(string? tenantId)
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "test-flag",
			TenantAccessControl = new AccessControl(rolloutPercentage: 50),
			Variations = new Variations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(criteria, context));
		exception.Message.ShouldBe("Tenant ID is required for percentage rollout evaluation.");
	}

	[Fact]
	public async Task ProcessEvaluation_TenantExplicitlyAllowed_ReturnsEnabled()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "test-flag",
			TenantAccessControl = new AccessControl(allowed: ["tenant123"]),
			Variations = new Variations {
				Values = new Dictionary<string, object> 
				{ 
					{ "on", true }, 
					{ "off", false } 
				},
				DefaultVariation = "disabled"
			}
		};

		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task ProcessEvaluation_TenantExplicitlyBlocked_ReturnsDisabled()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "test-flag",
			TenantAccessControl = new AccessControl(
				blocked: ["tenant123"],
				rolloutPercentage: 100), // Even with 100% rollout, blocked takes precedence
			Variations = new Variations { DefaultVariation = "blocked" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
	}

	[Fact]
	public async Task ProcessEvaluation_ZeroPercentRollout_ReturnsDisabled()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "zero-rollout-flag",
			TenantAccessControl = new AccessControl(rolloutPercentage: 0),
			Variations = new Variations { DefaultVariation = "zero-rollout" }
		};
		var context = new EvaluationContext(tenantId: "any-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("zero-rollout");
	}

	[Fact]
	public async Task ProcessEvaluation_PartialRollout_UsesConsistentHashing()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "partial-rollout-flag",
			TenantAccessControl = new AccessControl(rolloutPercentage: 50),
		};
		criteria.Variations.DefaultVariation = "not-in-rollout";
		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Act - Multiple evaluations should be consistent
		var result1 = await _evaluator.ProcessEvaluation(criteria, context);
		var result2 = await _evaluator.ProcessEvaluation(criteria, context);

		// Assert
		result1.IsEnabled.ShouldBe(result2.IsEnabled);
		result1.Variation.ShouldBe(result2.Variation);
		result1.Reason.ShouldBe(result2.Reason);

		// Verify reason format
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
	public async Task ProcessEvaluation_DifferentFlags_MayHaveDifferentResults()
	{
		// Arrange
		var criteria1 = new EvaluationCriteria
		{
			FlagKey = "flag-one",
			TenantAccessControl = new AccessControl(rolloutPercentage: 50),
		};
		criteria1.Variations.DefaultVariation = "flag1-off";
		var criteria2 = new EvaluationCriteria
		{
			FlagKey = "flag-two",
			TenantAccessControl = new AccessControl(rolloutPercentage: 50),
		};
		criteria2.Variations.DefaultVariation = "flag2-off";

		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Act
		var result1 = await _evaluator.ProcessEvaluation(criteria1, context);
		var result2 = await _evaluator.ProcessEvaluation(criteria2, context);

		// Assert - Results may differ due to different flag keys in hash
		result1.IsEnabled.ShouldBeOneOf(true, false);
		result2.IsEnabled.ShouldBeOneOf(true, false);

		// Verify variations match expected values
		if (result1.IsEnabled)
		{
			result1.Variation.ShouldBe("on");
		}
		else
		{
			result1.Variation.ShouldBe("flag1-off");
		}

		if (result2.IsEnabled)
		{
			result2.Variation.ShouldBe("on");
		}
		else
		{
			result2.Variation.ShouldBe("flag2-off");
		}
	}

	[Fact]
	public async Task ProcessEvaluation_EnterpriseScenario_WorksCorrectly()
	{
		// Arrange - Premium customers get early access, gradual rollout to others
		var criteria = new EvaluationCriteria
		{
			FlagKey = "enterprise-feature",
			TenantAccessControl = new AccessControl(
				allowed: ["premium-corp"],
				rolloutPercentage: 25),
		};
		criteria.Variations.DefaultVariation = "standard-feature";

		// Act & Assert - Premium customer gets access
		var premiumResult = await _evaluator.ProcessEvaluation(criteria,
			new EvaluationContext(tenantId: "premium-corp"));
		premiumResult.IsEnabled.ShouldBeTrue();
		premiumResult.Variation.ShouldBe("on");

		// Act & Assert - Regular tenant may or may not get access
		var regularResult = await _evaluator.ProcessEvaluation(criteria,
			new EvaluationContext(tenantId: "regular-tenant"));
		regularResult.IsEnabled.ShouldBeOneOf(true, false);

		if (regularResult.IsEnabled)
		{
			regularResult.Reason.ShouldContain(@"< 25%");
		}
		else
		{
			regularResult.Reason.ShouldContain(@">= 25%");
		}
	}

	// ===== NEW VARIATION SELECTION TESTS =====
	
	[Fact]
	public async Task ProcessEvaluation_SimpleOnOffFlag_ReturnsOnVariation()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "simple-tenant-flag",
			TenantAccessControl = new AccessControl(allowed: ["tenant123"]),
			Variations = new Variations 
			{ 
				Values = new Dictionary<string, object> { { "on", true }, { "off", false } },
				DefaultVariation = "off" 
			}
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(criteria, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task ProcessEvaluation_MultipleVariations_ReturnsConsistentVariationSelection()
	{
		// Arrange
		var criteria = new EvaluationCriteria
		{
			FlagKey = "database-engine",
			TenantAccessControl = new AccessControl(allowed: ["acme-corp"], rolloutPercentage: 100),
			Variations = new Variations 
			{ 
				Values = new Dictionary<string, object> 
				{
					{ "postgres", "postgresql" },
					{ "mysql", "mysql" }, 
					{ "cockroachdb", "cockroachdb" }
				},
				DefaultVariation = "mysql" 
			}
		};
		var context = new EvaluationContext(tenantId: "acme-corp");

		// Act - Multiple calls should return same variation
		var result1 = await _evaluator.ProcessEvaluation(criteria, context);
		var result2 = await _evaluator.ProcessEvaluation(criteria, context);

		// Assert
		result1.IsEnabled.ShouldBeTrue();
		result2.IsEnabled.ShouldBeTrue();
		result1.Variation.ShouldBe(result2.Variation);
		result1.Variation.ShouldBeOneOf("postgres", "mysql", "cockroachdb");
	}
}