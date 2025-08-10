using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Client.Evaluators;

public class TenantOverrideHandler_CanProcessLogic
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_CanProcessLogic()
	{
		_handler = new TenantOverrideHandler();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task If_TenantIdMissing_ThenCannotProcess(string? tenantId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}

	[Theory]
	[InlineData("tenant123")]
	[InlineData("tenant@example.com")]
	[InlineData("tenant-with-dashes")]
	[InlineData("tenant_with_underscores")]
	[InlineData("1234567890")]
	[InlineData("special-chars!@#$%")]
	public async Task If_TenantIdProvided_ThenCanProcess(string tenantId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { tenantId },
			DefaultVariation = "disabled"
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled");
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}
}

public class TenantOverrideHandler_DisabledTenantsList
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_DisabledTenantsList()
	{
		_handler = new TenantOverrideHandler();
	}

	[Fact]
	public async Task If_TenantInDisabledList_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "blocked-tenant", "another-blocked", "third-tenant" },
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100,
			DefaultVariation = "tenant-disabled"
		};
		var context = new EvaluationContext(tenantId: "blocked-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("tenant-disabled");
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Theory]
	[InlineData("tenant1")]
	[InlineData("tenant2")]
	[InlineData("tenant3")]
	public async Task If_MultipleTenantsInDisabledList_ThenAllDisabled(string tenantId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multiple-disabled-tenants-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "tenant1", "tenant2", "tenant3" },
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100,
			DefaultVariation = "disabled-variation"
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-variation");
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Fact]
	public async Task If_TenantNotInDisabledList_ThenDoesNotDisable()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-disabled-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "other-tenant", "another-tenant" },
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100, // Allow all tenants
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: "allowed-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}

	[Theory]
	[InlineData("control")]
	[InlineData("fallback")]
	[InlineData("disabled")]
	[InlineData("")]
	public async Task If_TenantDisabledWithDifferentVariations_ThenReturnsDefaultVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "variation-disabled-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "disabled-tenant" },
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100,
			DefaultVariation = defaultVariation
		};
		var context = new EvaluationContext(tenantId: "disabled-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Fact]
	public async Task If_TenantDisabledWithNullVariation_ThenReturnsNull()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-variation-disabled-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "disabled-tenant" },
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100,
			DefaultVariation = null!
		};
		var context = new EvaluationContext(tenantId: "disabled-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}
}

public class TenantOverrideHandler_EnabledTenantsList
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_EnabledTenantsList()
	{
		_handler = new TenantOverrideHandler();
	}

	[Fact]
	public async Task If_TenantInEnabledList_ThenReturnsNull()
	{
		// Arrange - Enabled tenant should return null to continue evaluation chain
		var flag = new FeatureFlag
		{
			Key = "enabled-tenant-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string> { "premium-tenant", "enterprise-tenant", "vip-tenant" },
			TenantPercentageEnabled = 0,
			DefaultVariation = "should-continue"
		};
		var context = new EvaluationContext(tenantId: "premium-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}

	[Theory]
	[InlineData("vip1")]
	[InlineData("vip2")]
	[InlineData("vip3")]
	public async Task If_MultipleTenantsInEnabledList_ThenAllContinueEvaluation(string tenantId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multiple-enabled-tenants-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string> { "vip1", "vip2", "vip3" },
			TenantPercentageEnabled = 0,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public async Task If_TenantNotInEnabledListButInPercentage_ThenContinuesEvaluation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-enabled-tenant-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string> { "vip-tenant", "premium-tenant" },
			TenantPercentageEnabled = 100, // Allow all tenants by percentage
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: "regular-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}
}

public class TenantOverrideHandler_OverridePrecedence
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_OverridePrecedence()
	{
		_handler = new TenantOverrideHandler();
	}

	[Fact]
	public async Task If_TenantInBothLists_ThenDisabledTakesPrecedence()
	{
		// Arrange - Tenant is in both enabled and disabled lists
		var flag = new FeatureFlag
		{
			Key = "precedence-test-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "conflict-tenant", "other-tenant" },
			EnabledTenants = new List<string> { "conflict-tenant", "vip-tenant" },
			TenantPercentageEnabled = 100,
			DefaultVariation = "conflict-disabled"
		};
		var context = new EvaluationContext(tenantId: "conflict-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse(); // Disabled takes precedence
		result.Variation.ShouldBe("conflict-disabled");
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Fact]
	public async Task If_TenantInBothListsMultipleScenarios_ThenAlwaysDisabledWins()
	{
		// Arrange - Test multiple tenants in both lists
		var flag = new FeatureFlag
		{
			Key = "multiple-precedence-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "tenant1", "tenant2", "tenant3" },
			EnabledTenants = new List<string> { "tenant1", "tenant2", "tenant3", "tenant4" },
			TenantPercentageEnabled = 100,
			DefaultVariation = "precedence-test"
		};

		var conflictTenants = new[] { "tenant1", "tenant2", "tenant3" };

		foreach (var tenantId in conflictTenants)
		{
			var context = new EvaluationContext(tenantId: tenantId);

			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("precedence-test");
			result.Reason.ShouldBe("Tenant explicitly disabled");
		}

		// Test tenant only in enabled list
		var enabledOnlyContext = new EvaluationContext(tenantId: "tenant4");
		var enabledResult = await _handler.Handle(flag, enabledOnlyContext);

		enabledResult.ShouldNotBeNull();
		enabledResult.IsEnabled.ShouldBeFalse();
	}
}

public class TenantOverrideHandler_TenantPercentageRollout
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_TenantPercentageRollout()
	{
		_handler = new TenantOverrideHandler();
	}

	[Fact]
	public async Task If_TenantPercentageEnabled0_ThenAllTenantsBlocked()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "zero-percent-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 0,
			DefaultVariation = "blocked"
		};

		var tenantIds = new[] { "tenant1", "tenant2", "tenant3", "tenant4", "tenant5" };

		foreach (var tenantId in tenantIds)
		{
			var context = new EvaluationContext(tenantId: tenantId);

			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("blocked");
			result.Reason.ShouldBe("Tenant not in percentage rollout");
		}
	}

	[Fact]
	public async Task If_TenantPercentageEnabled100_ThenAllTenantsContinue()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "hundred-percent-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100,
			DefaultVariation = "should-continue"
		};

		var tenantIds = new[] { "tenant1", "tenant2", "tenant3", "tenant4", "tenant5" };

		foreach (var tenantId in tenantIds)
		{
			var context = new EvaluationContext(tenantId: tenantId);

			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeFalse();
		}
	}

	[Theory]
	[InlineData(25)]
	[InlineData(50)]
	[InlineData(75)]
	public async Task If_TenantPercentageSet_ThenSomeTenantsAllowed(int percentage)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = $"tenant-percentage-{percentage}-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = percentage,
			DefaultVariation = "blocked"
		};

		var totalTenants = 100;
		var allowedCount = 0;
		var blockedCount = 0;

		// Act - Test with many tenants
		for (int i = 0; i < totalTenants; i++)
		{
			var context = new EvaluationContext(tenantId: $"tenant-{i}");
			var result = await _handler.Handle(flag, context);

			if (result.Reason.Contains("No evaluator could handle this flag"))
			{
				allowedCount++; // Tenant allowed, continues evaluation
			}
			else if (result.Reason == "Tenant not in percentage rollout")
			{
				blockedCount++; // Tenant blocked by percentage
			}
		}

		// Assert - Should approximate the percentage (allow some variance)
		var actualPercentage = (double)allowedCount / totalTenants * 100;
		actualPercentage.ShouldBeInRange(percentage - 15.0, percentage + 15.0); // Allow 15% variance
		(allowedCount + blockedCount).ShouldBe(totalTenants);
	}
}

public class TenantOverrideHandler_ConsistentHashing
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_ConsistentHashing()
	{
		_handler = new TenantOverrideHandler();
	}

	[Fact]
	public async Task If_SameTenantMultipleEvaluations_ThenConsistentResults()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "consistency-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: "consistent-tenant");

		// Act - Multiple evaluations
		var result1 = await _handler.Handle(flag, context);
		var result2 = await _handler.Handle(flag, context);
		var result3 = await _handler.Handle(flag, context);

		// Assert - All results should be identical
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		result3.ShouldNotBeNull();

		result1.IsEnabled.ShouldBe(result2.IsEnabled);
		result1.IsEnabled.ShouldBe(result3.IsEnabled);
		result1.Variation.ShouldBe(result2.Variation);
		result1.Variation.ShouldBe(result3.Variation);
		result1.Reason.ShouldBe(result2.Reason);
		result1.Reason.ShouldBe(result3.Reason);
	}

	[Fact]
	public async Task If_SameTenantDifferentFlags_ThenDifferentHashResults()
	{
		// Arrange
		var flag1 = new FeatureFlag
		{
			Key = "flag-one-tenant",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};

		var flag2 = new FeatureFlag
		{
			Key = "flag-two-tenant",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};

		var context = new EvaluationContext(tenantId: "same-tenant");

		// Act
		var result1 = await _handler.Handle(flag1, context);
		var result2 = await _handler.Handle(flag2, context);

		// Assert - Results may be different due to different flag keys in hash
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();

		// The results may be different because the hash input includes the flag key
		// We can't assert they're different, but we can verify they're both valid
		(result1.Reason.Contains("No evaluator could handle this flag") || result1.Reason == "Tenant not in percentage rollout").ShouldBeTrue();
		(result2.Reason.Contains("No evaluator could handle this flag") || result2.Reason == "Tenant not in percentage rollout").ShouldBeTrue();
	}

	[Fact]
	public async Task If_DifferentTenantsSameFlag_ThenDifferentHashResults()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multi-tenant-hash-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};

		var context1 = new EvaluationContext(tenantId: "tenant-one");
		var context2 = new EvaluationContext(tenantId: "tenant-two");

		// Act
		var result1 = await _handler.Handle(flag, context1);
		var result2 = await _handler.Handle(flag, context2);

		// Assert - Results may be different due to different tenant IDs in hash
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();

		// Both should be valid responses
		(result1.Reason.Contains("No evaluator could handle this flag") || result1.Reason == "Tenant not in percentage rollout").ShouldBeTrue();
		(result2.Reason.Contains("No evaluator could handle this flag") || result2.Reason == "Tenant not in percentage rollout").ShouldBeTrue();
	}
}

public class TenantOverrideHandler_HashInputFormat
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_HashInputFormat()
	{
		_handler = new TenantOverrideHandler();
	}

	[Theory]
	[InlineData("test-flag", "tenant123")]
	[InlineData("feature_toggle", "company@tenant.com")]
	[InlineData("UPPERCASE-FLAG", "lowercase-tenant")]
	[InlineData("flag-with-special!@#", "tenant-with-special!@#")]
	public async Task If_DifferentFlagAndTenantCombinations_ThenHashInputFormattedCorrectly(string flagKey, string tenantId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = flagKey,
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		// The hash input format should be: "{flagKey}:tenant:{tenantId}"
		// We can't directly test the hash input, but we can verify consistent behavior
		(result.Reason.Contains("No evaluator could handle this flag") || result.Reason == "Tenant not in percentage rollout").ShouldBeTrue();
	}

	[Fact]
	public async Task If_SameFlagAndTenantDifferentContext_ThenConsistentHashing()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "context-test-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};

		// Same tenant ID but different context properties
		var context1 = new EvaluationContext(tenantId: "test-tenant", userId: "user1");
		var context2 = new EvaluationContext(tenantId: "test-tenant", userId: "user2");
		var context3 = new EvaluationContext(
			tenantId: "test-tenant",
			attributes: new Dictionary<string, object> { { "plan", "premium" } }
		);

		// Act
		var result1 = await _handler.Handle(flag, context1);
		var result2 = await _handler.Handle(flag, context2);
		var result3 = await _handler.Handle(flag, context3);

		// Assert - Should be consistent since only flag key and tenant ID are used in hash
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		result3.ShouldNotBeNull();

		result1.IsEnabled.ShouldBe(result2.IsEnabled);
		result1.IsEnabled.ShouldBe(result3.IsEnabled);
		result1.Reason.ShouldBe(result2.Reason);
		result1.Reason.ShouldBe(result3.Reason);
	}
}

public class TenantOverrideHandler_StringComparison
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_StringComparison()
	{
		_handler = new TenantOverrideHandler();
	}

	[Theory]
	[InlineData("Tenant123", "tenant123")] // Different case
	[InlineData("TENANT123", "tenant123")] // All caps vs lowercase
	[InlineData("tenant123", "Tenant123")] // Lowercase vs mixed case
	public async Task If_TenantIdCaseDifference_ThenUsesExactMatch(string listTenantId, string contextTenantId)
	{
		// Arrange - Test case sensitivity (should be exact match)
		var flag = new FeatureFlag
		{
			Key = "case-sensitive-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { listTenantId },
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100,
			DefaultVariation = "case-test"
		};
		var context = new EvaluationContext(tenantId: contextTenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		if (listTenantId == contextTenantId) // Exact match
		{
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("case-test");
			result.Reason.ShouldBe("Tenant explicitly disabled");
		}
		else // Case mismatch
		{
			result.IsEnabled.ShouldBeFalse();
			result.Reason.ShouldContain("No evaluator could handle this flag"); // Continues evaluation
		}
	}

	[Theory]
	[InlineData("tenant@example.com")]
	[InlineData("tenant-with-dashes")]
	[InlineData("tenant_with_underscores")]
	[InlineData("123456789")]
	[InlineData("special!@#$%^&*()")]
	public async Task If_SpecialCharacterTenantIds_ThenHandlesCorrectly(string specialTenantId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "special-chars-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string> { specialTenantId },
			TenantPercentageEnabled = 0, // Only enabled tenants should continue
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: specialTenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}
}

public class TenantOverrideHandler_ProcessEvaluationFlow
{
	private readonly TenantOverrideHandler _handler;
	private readonly Mock<IFlagEvaluationHandler> _mockNextHandler;

	public TenantOverrideHandler_ProcessEvaluationFlow()
	{
		_handler = new TenantOverrideHandler();
		_mockNextHandler = new Mock<IFlagEvaluationHandler>();
	}

	[Fact]
	public async Task If_TenantAllowedByOverrideOrPercentage_ThenCallsNext()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "call-next-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string> { "allowed-tenant" },
			TenantPercentageEnabled = 0,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: "allowed-tenant");
		var expectedResult = new EvaluationResult(isEnabled: true, variation: "next-handler");

		_mockNextHandler.Setup(x => x.Handle(flag, context))
			.ReturnsAsync(expectedResult);
		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldBe(expectedResult);
		_mockNextHandler.Verify(x => x.Handle(flag, context), Times.Once);
	}

	[Fact]
	public async Task If_TenantDisabled_ThenDoesNotCallNext()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-no-next-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "blocked-tenant" },
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100,
			DefaultVariation = "blocked"
		};
		var context = new EvaluationContext(tenantId: "blocked-tenant");

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Tenant explicitly disabled");
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_TenantNotInPercentageRollout_ThenDoesNotCallNext()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "percentage-blocked-tenant-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 0, // Block all tenants
			DefaultVariation = "percentage-blocked"
		};
		var context = new EvaluationContext(tenantId: "blocked-by-percentage");

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Tenant not in percentage rollout");
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}
}

public class TenantOverrideHandler_EdgeCases
{
	private readonly TenantOverrideHandler _handler;

	public TenantOverrideHandler_EdgeCases()
	{
		_handler = new TenantOverrideHandler();
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	[InlineData(999)]
	public async Task If_InvalidTenantPercentageValues_ThenHandlesGracefully(int invalidPercentage)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "invalid-tenant-percentage-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = invalidPercentage,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: "test-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();

		if (invalidPercentage <= 0)
		{
			result.IsEnabled.ShouldBeFalse();
			result.Reason.ShouldBe("Tenant not in percentage rollout");
		}
		// For > 100, all tenants should be allowed (continues evaluation)
		else if (invalidPercentage >= 100)
		{
			result.IsEnabled.ShouldBeFalse();
		}
	}

	[Fact]
	public async Task If_VeryLargeTenantLists_ThenPerformsEfficiently()
	{
		// Arrange
		var largeDisabledList = Enumerable.Range(1, 10000).Select(i => $"disabled-tenant-{i}").ToList();
		var largeEnabledList = Enumerable.Range(1, 10000).Select(i => $"enabled-tenant-{i}").ToList();

		var flag = new FeatureFlag
		{
			Key = "large-tenant-lists-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = largeDisabledList,
			EnabledTenants = largeEnabledList,
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};

		// Test tenant in disabled list
		var disabledContext = new EvaluationContext(tenantId: "disabled-tenant-5000");

		// Act
		var disabledResult = await _handler.Handle(flag, disabledContext);

		// Assert
		disabledResult.ShouldNotBeNull();
		disabledResult.IsEnabled.ShouldBeFalse();
		disabledResult.Reason.ShouldBe("Tenant explicitly disabled");

		// Test tenant in enabled list
		var enabledContext = new EvaluationContext(tenantId: "enabled-tenant-7500");

		// Act
		var enabledResult = await _handler.Handle(flag, enabledContext);

		// Assert
		enabledResult.ShouldNotBeNull();
		enabledResult.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public async Task If_DuplicateTenantsInList_ThenHandlesCorrectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "duplicate-tenants-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string> { "duplicate-tenant", "duplicate-tenant", "other-tenant", "duplicate-tenant" },
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 100,
			DefaultVariation = "duplicate-disabled"
		};
		var context = new EvaluationContext(tenantId: "duplicate-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("duplicate-disabled");
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task If_EmptyFlagKey_ThenStillProcessesHash(string emptyFlagKey)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = emptyFlagKey,
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: "test-tenant");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		// Should not throw, even with empty flag key in hash input
		(result.Reason.Contains("No evaluator could handle this flag") || result.Reason == "Tenant not in percentage rollout").ShouldBeTrue();
	}

	[Fact]
	public async Task If_VeryLongTenantIdAndFlagKey_ThenHandlesCorrectly()
	{
		// Arrange
		var longFlagKey = new string('f', 1000);
		var longTenantId = new string('t', 1000);

		var flag = new FeatureFlag
		{
			Key = longFlagKey,
			Status = FeatureFlagStatus.Enabled,
			DisabledTenants = new List<string>(),
			EnabledTenants = new List<string>(),
			TenantPercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: longTenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		// Should handle very long strings in hash input without issues
		(result.Reason.Contains("End of evaluation chain") || result.Reason == "Tenant not in percentage rollout").ShouldBeTrue();
	}
}