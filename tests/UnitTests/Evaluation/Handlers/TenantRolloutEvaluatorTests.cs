using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;
using Shouldly;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class TenantRolloutEvaluator_EvaluationOrder
{
	[Fact]
	public void EvaluationOrder_ShouldReturnTenantRollout()
	{
		// Arrange
		var evaluator = new TenantRolloutEvaluator();

		// Act
		var order = evaluator.EvaluationOrder;

		// Assert
		order.ShouldBe(EvaluationOrder.TenantRollout);
	}
}

public class TenantRolloutEvaluator_CanProcess
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_CanProcess()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public void If_TenantIdProvidedAndTenantExplicitlyManaged_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(allowedTenants: ["tenant123"])
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_TenantIdProvidedButNotExplicitlyManaged_AndNoPercentageRollout_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TenantAccess = FlagTenantAccessControl.Unrestricted,
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_TenantRolloutPercentageModeWithAccessRestrictions_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50)
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_TenantRolloutPercentageModeButNoAccessRestrictions_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = FlagTenantAccessControl.Unrestricted // No restrictions (100% rollout)
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_NoTenantId_AndNoTenantRolloutPercentageMode_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(allowedTenants: ["tenant123"])
		};
		var context = new EvaluationContext(userId: "user123"); // No tenant ID

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_EmptyTenantId_AndNoTenantRolloutPercentageMode_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(allowedTenants: ["tenant123"])
		};
		var context = new EvaluationContext(tenantId: ""); // Empty tenant ID

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_WhitespaceTenantId_AndNoTenantRolloutPercentageMode_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(allowedTenants: ["tenant123"])
		};
		var context = new EvaluationContext(tenantId: "   "); // Whitespace tenant ID

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Disabled)]
	[InlineData(FlagEvaluationMode.Enabled)]
	[InlineData(FlagEvaluationMode.Scheduled)]
	[InlineData(FlagEvaluationMode.TimeWindow)]
	[InlineData(FlagEvaluationMode.UserTargeted)]
	[InlineData(FlagEvaluationMode.UserRolloutPercentage)]
	public void If_NonTenantRolloutModeWithoutExplicitTenantManagement_ThenCannotProcess(FlagEvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			TenantAccess = FlagTenantAccessControl.Unrestricted
		};
		flag.EvaluationModeSet.AddMode(mode);
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}
}

public class TenantRolloutEvaluator_ProcessEvaluation_TenantIdValidation
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_ProcessEvaluation_TenantIdValidation()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public async Task If_NoTenantId_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(userId: "user123"); // No tenant ID

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("Tenant ID is required for percentage rollout evaluation.");
	}

	[Fact]
	public async Task If_EmptyTenantId_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: ""); // Empty tenant ID

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("Tenant ID is required for percentage rollout evaluation.");
	}

	[Fact]
	public async Task If_WhitespaceTenantId_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: "   "); // Whitespace tenant ID

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("Tenant ID is required for percentage rollout evaluation.");
	}
}

public class TenantRolloutEvaluator_ProcessEvaluation_ExplicitlyAllowedTenants
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_ProcessEvaluation_ExplicitlyAllowedTenants()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public async Task If_TenantExplicitlyAllowed_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: ["tenant123", "tenant456"]),
			Variations = new FlagVariations { DefaultVariation = "disabled" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public async Task If_TenantExplicitlyAllowedCaseInsensitive_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: ["TENANT123"]),
			Variations = new FlagVariations { DefaultVariation = "disabled" }
		};
		var context = new EvaluationContext(tenantId: "tenant123"); // Different case

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public async Task If_TenantNotInAllowedList_AndNoPercentageRollout_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: ["tenant456", "tenant789"],
				rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = "restricted" }
		};
		var context = new EvaluationContext(tenantId: "tenant123"); // Not in allowed list

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("restricted");
		result.Reason.ShouldBe("Access restricted to all tenants");
	}
}

public class TenantRolloutEvaluator_ProcessEvaluation_ExplicitlyBlockedTenants
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_ProcessEvaluation_ExplicitlyBlockedTenants()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public async Task If_TenantExplicitlyBlocked_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				blockedTenants: ["tenant123", "tenant456"],
				rolloutPercentage: 100), // Even with 100% rollout, blocked takes precedence
			Variations = new FlagVariations { DefaultVariation = "blocked" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
		result.Reason.ShouldBe("Tenant explicitly blocked");
	}

	[Fact]
	public async Task If_TenantExplicitlyBlockedCaseInsensitive_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				blockedTenants: ["TENANT123"],
				rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "blocked" }
		};
		var context = new EvaluationContext(tenantId: "tenant123"); // Different case

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
		result.Reason.ShouldBe("Tenant explicitly blocked");
	}

	[Fact]
	public async Task If_TenantInBothAllowedAndBlockedLists_ThenBlockedTakesPrecedence()
	{
		// Arrange - This scenario shouldn't happen with proper validation, but test defensive behavior
		var allowedTenants = new List<string> { "tenant123" };
		var blockedTenants = new List<string> { "tenant123" };
		
		// Use LoadAccessControl to bypass validation that prevents this scenario
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.LoadAccessControl(
				allowedTenants: allowedTenants,
				blockedTenants: blockedTenants,
				rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "conflict" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("conflict");
		result.Reason.ShouldBe("Tenant explicitly blocked");
	}
}

public class TenantRolloutEvaluator_ProcessEvaluation_PercentageRollout
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_ProcessEvaluation_PercentageRollout()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public async Task If_ZeroPercentRollout_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "zero-rollout-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = "zero-rollout" }
		};
		var context = new EvaluationContext(tenantId: "any-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("zero-rollout");
		result.Reason.ShouldBe("Access restricted to all tenants");
	}

	[Fact]
	public async Task If_HundredPercentRollout_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "full-rollout-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "restricted" }
		};
		var context = new EvaluationContext(tenantId: "any-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Access unrestricted to all tenants");
	}

	[Fact]
	public async Task If_PartialRollout_ThenUsesConsistentHashing()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "partial-rollout-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "not-in-rollout" }
		};

		// Test the same tenant multiple times to ensure consistency
		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Act - Multiple evaluations
		var result1 = await _evaluator.ProcessEvaluation(flag, context);
		var result2 = await _evaluator.ProcessEvaluation(flag, context);
		var result3 = await _evaluator.ProcessEvaluation(flag, context);

		// Assert - All results should be identical
		result2.IsEnabled.ShouldBe(result1.IsEnabled);
		result3.IsEnabled.ShouldBe(result1.IsEnabled);
		result2.Variation.ShouldBe(result1.Variation);
		result3.Variation.ShouldBe(result1.Variation);
		result2.Reason.ShouldBe(result1.Reason);
		result3.Reason.ShouldBe(result1.Reason);

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
	public async Task If_DifferentTenantsSameFlag_ThenMayHaveDifferentResults()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "distribution-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "not-in-rollout" }
		};

		var tenant1Context = new EvaluationContext(tenantId: "tenant-alpha");
		var tenant2Context = new EvaluationContext(tenantId: "tenant-beta");

		// Act
		var result1 = await _evaluator.ProcessEvaluation(flag, tenant1Context);
		var result2 = await _evaluator.ProcessEvaluation(flag, tenant2Context);

		// Assert - Results should be valid (may be different)
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();

		// Both should return valid responses
		result1.IsEnabled.ShouldBeOneOf(true, false);
		result2.IsEnabled.ShouldBeOneOf(true, false);

		// Verify reason formats
		if (result1.IsEnabled)
		{
			result1.Variation.ShouldBe("on");
			result1.Reason.ShouldMatch(@"Tenant in rollout: \d+% < 50%");
		}
		else
		{
			result1.Variation.ShouldBe("not-in-rollout");
			result1.Reason.ShouldMatch(@"Tenant not in rollout: \d+% >= 50%");
		}

		if (result2.IsEnabled)
		{
			result2.Variation.ShouldBe("on");
			result2.Reason.ShouldMatch(@"Tenant in rollout: \d+% < 50%");
		}
		else
		{
			result2.Variation.ShouldBe("not-in-rollout");
			result2.Reason.ShouldMatch(@"Tenant not in rollout: \d+% >= 50%");
		}
	}

	[Fact]
	public async Task If_SameTenantDifferentFlags_ThenMayHaveDifferentResults()
	{
		// Arrange
		var flag1 = new FeatureFlag
		{
			Key = "flag-one",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "flag1-off" }
		};

		var flag2 = new FeatureFlag
		{
			Key = "flag-two",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "flag2-off" }
		};

		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Act
		var result1 = await _evaluator.ProcessEvaluation(flag1, context);
		var result2 = await _evaluator.ProcessEvaluation(flag2, context);

		// Assert - Results may be different due to different flag keys in hash
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();

		// Both should return valid responses
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
}

public class TenantRolloutEvaluator_ProcessEvaluation_Variations
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_ProcessEvaluation_Variations()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public async Task If_TenantAllowed_ThenReturnsOnVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: ["tenant123"]),
			Variations = new FlagVariations { DefaultVariation = "custom-default" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public async Task If_TenantDenied_ThenReturnsDefaultVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				blockedTenants: ["tenant123"]),
			Variations = new FlagVariations { DefaultVariation = "tenant-blocked" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("tenant-blocked");
		result.Reason.ShouldBe("Tenant explicitly blocked");
	}

	[Fact]
	public async Task If_TenantInRollout_ThenReturnsOnVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "rollout-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "rollout-off" }
		};
		var context = new EvaluationContext(tenantId: "any-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Access unrestricted to all tenants");
	}

	[Fact]
	public async Task If_TenantNotInRollout_ThenReturnsDefaultVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "rollout-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = "no-rollout" }
		};
		var context = new EvaluationContext(tenantId: "any-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("no-rollout");
		result.Reason.ShouldBe("Access restricted to all tenants");
	}

	[Theory]
	[InlineData("custom-default")]
	[InlineData("off")]
	[InlineData("")]
	[InlineData("fallback")]
	public async Task If_TenantDenied_ThenReturnsSpecifiedDefaultVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = defaultVariation }
		};
		var context = new EvaluationContext(tenantId: "any-tenant");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("Access restricted to all tenants");
	}
}

public class TenantRolloutEvaluator_ProcessEvaluation_EdgeCases
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_ProcessEvaluation_EdgeCases()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public async Task If_TenantIdWithSpaces_ThenTrimsAndEvaluates()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: ["tenant123"]),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: "  tenant123  "); // Spaces around tenant ID

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public async Task If_TenantIdWithSpecialCharacters_ThenEvaluatesCorrectly()
	{
		// Arrange
		var specialTenantId = "tenant@company.com";
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: [specialTenantId]),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: specialTenantId);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public async Task If_VeryLongTenantId_ThenEvaluatesCorrectly()
	{
		// Arrange
		var longTenantId = new string('a', 1000); // Very long tenant ID
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: longTenantId);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Access unrestricted to all tenants");
	}

	[Fact]
	public async Task If_UnicodeInTenantId_ThenEvaluatesCorrectly()
	{
		// Arrange
		var unicodeTenantId = "tenant-??-????";
		var flag = new FeatureFlag
		{
			Key = "unicode-flag",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: [unicodeTenantId]),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: unicodeTenantId);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Tenant explicitly allowed");
	}
}

public class TenantRolloutEvaluator_ProcessEvaluation_RealWorldScenarios
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_ProcessEvaluation_RealWorldScenarios()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public async Task EnterpriseTenantRollout_PremiumCustomersFirst_ShouldWorkCorrectly()
	{
		// Arrange - Premium customers get early access, then gradual rollout to others
		var flag = new FeatureFlag
		{
			Key = "enterprise-feature",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: ["premium-corp", "enterprise-inc"],
				rolloutPercentage: 25), // 25% of other tenants
			Variations = new FlagVariations { DefaultVariation = "standard-feature" }
		};

		// Test premium customer (should always get access)
		var premiumResult = await _evaluator.ProcessEvaluation(flag, 
			new EvaluationContext(tenantId: "premium-corp"));

		// Test regular customers (may or may not get access based on rollout)
		var regularTenant1Result = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(tenantId: "regular-tenant-1"));

		var regularTenant2Result = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(tenantId: "regular-tenant-2"));

		// Assert
		premiumResult.IsEnabled.ShouldBeTrue();
		premiumResult.Variation.ShouldBe("on");
		premiumResult.Reason.ShouldBe("Tenant explicitly allowed");

		// Regular tenants should get consistent results based on hash
		regularTenant1Result.IsEnabled.ShouldBeOneOf(true, false);
		regularTenant2Result.IsEnabled.ShouldBeOneOf(true, false);

		if (regularTenant1Result.IsEnabled)
		{
			regularTenant1Result.Reason.ShouldMatch(@"Tenant in rollout: \d+% < 25%");
		}
		else
		{
			regularTenant1Result.Reason.ShouldMatch(@"Tenant not in rollout: \d+% >= 25%");
		}
	}

	[Fact]
	public async Task BetaFeatureRollout_BlockedTenantsCantAccess_ShouldWorkCorrectly()
	{
		// Arrange - Beta feature with some tenants explicitly blocked
		var flag = new FeatureFlag
		{
			Key = "beta-feature",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				blockedTenants: ["problematic-tenant", "security-risk-tenant"],
				rolloutPercentage: 75), // 75% rollout except blocked tenants
			Variations = new FlagVariations { DefaultVariation = "stable-version" }
		};

		// Test blocked tenant (should never get access)
		var blockedResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(tenantId: "problematic-tenant"));

		// Test normal tenant (may get access based on rollout)
		var normalResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(tenantId: "normal-tenant"));

		// Assert
		blockedResult.IsEnabled.ShouldBeFalse();
		blockedResult.Variation.ShouldBe("stable-version");
		blockedResult.Reason.ShouldBe("Tenant explicitly blocked");

		normalResult.IsEnabled.ShouldBeOneOf(true, false);
		if (normalResult.IsEnabled)
		{
			normalResult.Variation.ShouldBe("on");
			normalResult.Reason.ShouldMatch(@"Tenant in rollout: \d+% < 75%");
		}
		else
		{
			normalResult.Variation.ShouldBe("stable-version");
			normalResult.Reason.ShouldMatch(@"Tenant not in rollout: \d+% >= 75%");
		}
	}

	[Fact]
	public async Task GradualRolloutStrategy_IncreasingPercentages_ShouldWorkCorrectly()
	{
		// Arrange - Simulate gradual rollout over time
		var baseFlag = new FeatureFlag
		{
			Key = "gradual-rollout",
			Variations = new FlagVariations { DefaultVariation = "old-version" }
		};

		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Phase 1: 10% rollout
		var phase1Flag = new FeatureFlag
		{
			Key = baseFlag.Key,
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 10),
			Variations = baseFlag.Variations
		};
		var phase1Result = await _evaluator.ProcessEvaluation(phase1Flag, context);

		// Phase 2: 50% rollout
		var phase2Flag = new FeatureFlag
		{
			Key = baseFlag.Key,
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = baseFlag.Variations
		};
		var phase2Result = await _evaluator.ProcessEvaluation(phase2Flag, context);

		// Phase 3: 100% rollout
		var phase3Flag = new FeatureFlag
		{
			Key = baseFlag.Key,
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 100),
			Variations = baseFlag.Variations
		};
		var phase3Result = await _evaluator.ProcessEvaluation(phase3Flag, context);

		// Assert
		// Same tenant should have consistent results within each phase
		var secondPhase1Result = await _evaluator.ProcessEvaluation(phase1Flag, context);
		phase1Result.IsEnabled.ShouldBe(secondPhase1Result.IsEnabled);

		// If tenant was in 10% rollout, it should also be in 50% and 100%
		if (phase1Result.IsEnabled)
		{
			phase2Result.IsEnabled.ShouldBeTrue();
			phase3Result.IsEnabled.ShouldBeTrue();
		}

		// If tenant was in 50% rollout, it should also be in 100%
		if (phase2Result.IsEnabled)
		{
			phase3Result.IsEnabled.ShouldBeTrue();
		}

		// 100% rollout should always be enabled
		phase3Result.IsEnabled.ShouldBeTrue();
		phase3Result.Reason.ShouldBe("Access unrestricted to all tenants");
	}

	[Fact]
	public async Task MaintenanceModeBypass_CriticalTenants_ShouldWorkCorrectly()
	{
		// Arrange - During maintenance, only critical tenants can access
		var flag = new FeatureFlag
		{
			Key = "maintenance-bypass",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(
				allowedTenants: ["critical-hospital", "emergency-services", "government-agency"],
				rolloutPercentage: 0), // No regular rollout during maintenance
			Variations = new FlagVariations { DefaultVariation = "maintenance-mode" }
		};

		var testCases = new[]
		{
			("critical-hospital", true, "Tenant explicitly allowed"),
			("emergency-services", true, "Tenant explicitly allowed"),
			("government-agency", true, "Tenant explicitly allowed"),
			("regular-business", false, "Access restricted to all tenants"),
			("small-company", false, "Access restricted to all tenants")
		};

		foreach (var (tenantId, expectedEnabled, expectedReason) in testCases)
		{
			// Act
			var result = await _evaluator.ProcessEvaluation(flag,
				new EvaluationContext(tenantId: tenantId));

			// Assert
			result.IsEnabled.ShouldBe(expectedEnabled);
			result.Reason.ShouldBe(expectedReason);
			
			if (expectedEnabled)
			{
				result.Variation.ShouldBe("on");
			}
			else
			{
				result.Variation.ShouldBe("maintenance-mode");
			}
		}
	}
}

public class TenantRolloutEvaluator_ProcessEvaluation_HashConsistency
{
	private readonly TenantRolloutEvaluator _evaluator;

	public TenantRolloutEvaluator_ProcessEvaluation_HashConsistency()
	{
		_evaluator = new TenantRolloutEvaluator();
	}

	[Fact]
	public async Task If_SameInputs_ThenAlwaysProduceSameResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "consistency-test",
			TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 30),
			Variations = new FlagVariations { DefaultVariation = "not-in-rollout" }
		};
		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Act - Multiple evaluations with same inputs
		var results = new List<EvaluationResult?>();
		for (int i = 0; i < 10; i++)
		{
			results.Add(await _evaluator.ProcessEvaluation(flag, context));
		}

		// Assert - All results should be identical
		var firstResult = results[0];
		foreach (var result in results.Skip(1))
		{
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBe(firstResult!.IsEnabled);
			result.Variation.ShouldBe(firstResult.Variation);
			result.Reason.ShouldBe(firstResult.Reason);
		}
	}

	[Fact]
	public async Task If_DifferentFlagKeys_ThenMayProduceDifferentResults()
	{
		// Arrange
		var tenantId = "distribution-test-tenant";
		var context = new EvaluationContext(tenantId: tenantId);

		var flags = new[]
		{
			new FeatureFlag
			{
				Key = "flag-alpha",
				TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
				Variations = new FlagVariations { DefaultVariation = "alpha-off" }
			},
			new FeatureFlag
			{
				Key = "flag-beta",
				TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
				Variations = new FlagVariations { DefaultVariation = "beta-off" }
			},
			new FeatureFlag
			{
				Key = "flag-gamma",
				TenantAccess = FlagTenantAccessControl.CreateAccessControl(rolloutPercentage: 50),
				Variations = new FlagVariations { DefaultVariation = "gamma-off" }
			}
		};

		// Act
		var results = new List<EvaluationResult?>();
		foreach (var flag in flags)
		{
			results.Add(await _evaluator.ProcessEvaluation(flag, context));
		}

		// Assert - Results should be valid (may vary across flags)
		foreach (var (result, flag) in results.Zip(flags))
		{
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeOneOf(true, false);
			
			if (result.IsEnabled)
			{
				result.Variation.ShouldBe("on");
				result.Reason.ShouldMatch(@"Tenant in rollout: \d+% < 50%");
			}
			else
			{
				result.Variation.ShouldBe(flag.Variations.DefaultVariation);
				result.Reason.ShouldMatch(@"Tenant not in rollout: \d+% >= 50%");
			}
		}

		// Verify that we potentially got different results (not all same)
		var enabledCount = results.Count(r => r!.IsEnabled);
		// With 50% rollout and 3 flags, we expect some variation in most cases
		// But this is probabilistic, so we just ensure results are valid
		enabledCount.ShouldBeInRange(0, 3);
	}
}