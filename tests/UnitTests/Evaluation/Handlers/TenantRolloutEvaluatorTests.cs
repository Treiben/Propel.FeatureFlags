using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class TenantRolloutEvaluator_CanProcess
{
	private readonly TenantRolloutEvaluator _evaluator = new();

	[Fact]
	public void CanProcess_TenantExplicitlyManaged_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TenantAccess = new FlagTenantAccessControl(allowedTenants: ["tenant123"])
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_TenantRolloutModeWithRestrictions_ReturnsTrue()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = new FlagTenantAccessControl(rolloutPercentage: 50)
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeTrue();
	}

	[Fact]
	public void CanProcess_TenantRolloutModeButNoRestrictions_ReturnsFalse()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = FlagTenantAccessControl.Unrestricted // No restrictions
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeFalse();
	}

	[Fact]
	public void CanProcess_NoTenantIdAndNoTenantMode_ReturnsFalse()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = new FlagTenantAccessControl(allowedTenants: ["tenant123"])
		};
		var context = new EvaluationContext(userId: "user123"); // No tenant ID

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void CanProcess_InvalidTenantId_ReturnsFalse(string? tenantId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TenantAccess = new FlagTenantAccessControl(allowedTenants: ["tenant123"])
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeFalse();
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Disabled)]
	[InlineData(FlagEvaluationMode.Enabled)]
	[InlineData(FlagEvaluationMode.Scheduled)]
	[InlineData(FlagEvaluationMode.UserTargeted)]
	public void CanProcess_NonTenantModesWithoutExplicitManagement_ReturnsFalse(FlagEvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = FlagTenantAccessControl.Unrestricted
		};
		flag.EvaluationModeSet.AddMode(mode);
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act & Assert
		_evaluator.CanProcess(flag, context).ShouldBeFalse();
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
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = new FlagTenantAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("Tenant ID is required for percentage rollout evaluation.");
	}

	[Fact]
	public async Task ProcessEvaluation_TenantExplicitlyAllowed_ReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = new FlagTenantAccessControl(allowedTenants: ["tenant123"]),
			Variations = new FlagVariations { DefaultVariation = "disabled" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public async Task ProcessEvaluation_TenantExplicitlyBlocked_ReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = new FlagTenantAccessControl(
				blockedTenants: ["tenant123"],
				rolloutPercentage: 100), // Even with 100% rollout, blocked takes precedence
			Variations = new FlagVariations { DefaultVariation = "blocked" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
		result.Reason.ShouldBe("Tenant explicitly blocked");
	}

	[Fact]
	public async Task ProcessEvaluation_ZeroPercentRollout_ReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "zero-rollout-flag",
			TenantAccess = new FlagTenantAccessControl(rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = "zero-rollout" }
		};
		var context = new EvaluationContext(tenantId: "any-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("zero-rollout");
		result.Reason.ShouldBe("Access restricted to all tenants");
	}

	[Fact]
	public async Task ProcessEvaluation_HundredPercentRollout_ReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "full-rollout-flag",
			TenantAccess = new FlagTenantAccessControl(rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "restricted" }
		};
		var context = new EvaluationContext(tenantId: "any-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Access unrestricted to all tenants");
	}

	[Fact]
	public async Task ProcessEvaluation_PartialRollout_UsesConsistentHashing()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "partial-rollout-flag",
			TenantAccess = new FlagTenantAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "not-in-rollout" }
		};
		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Act - Multiple evaluations should be consistent
		var result1 = await _evaluator.ProcessEvaluation(flag, context);
		var result2 = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result1.IsEnabled.ShouldBe(result2.IsEnabled);
		result1.Variation.ShouldBe(result2.Variation);
		result1.Reason.ShouldBe(result2.Reason);

		// Verify reason format
		if (result1.IsEnabled)
		{
			result1.Reason.ShouldMatch(@"Tenant in rollout: \d+% < 50%");
		}
		else
		{
			result1.Reason.ShouldMatch(@"Tenant not in rollout: \d+% >= 50%");
		}
	}

	[Fact]
	public async Task ProcessEvaluation_DifferentFlags_MayHaveDifferentResults()
	{
		// Arrange
		var flag1 = new FeatureFlag
		{
			Key = "flag-one",
			TenantAccess = new FlagTenantAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "flag1-off" }
		};
		var flag2 = new FeatureFlag
		{
			Key = "flag-two",
			TenantAccess = new FlagTenantAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "flag2-off" }
		};
		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Act
		var result1 = await _evaluator.ProcessEvaluation(flag1, context);
		var result2 = await _evaluator.ProcessEvaluation(flag2, context);

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
		var flag = new FeatureFlag
		{
			Key = "enterprise-feature",
			TenantAccess = new FlagTenantAccessControl(
				allowedTenants: ["premium-corp"],
				rolloutPercentage: 25),
			Variations = new FlagVariations { DefaultVariation = "standard-feature" }
		};

		// Act & Assert - Premium customer gets access
		var premiumResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(tenantId: "premium-corp"));
		premiumResult.IsEnabled.ShouldBeTrue();
		premiumResult.Variation.ShouldBe("on");
		premiumResult.Reason.ShouldBe("Tenant explicitly allowed");

		// Act & Assert - Regular tenant may or may not get access
		var regularResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(tenantId: "regular-tenant"));
		regularResult.IsEnabled.ShouldBeOneOf(true, false);

		if (regularResult.IsEnabled)
		{
			regularResult.Reason.ShouldMatch(@"Tenant in rollout: \d+% < 25%");
		}
		else
		{
			regularResult.Reason.ShouldMatch(@"Tenant not in rollout: \d+% >= 25%");
		}
	}
}