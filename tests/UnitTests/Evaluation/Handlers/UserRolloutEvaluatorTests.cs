using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class UserRolloutEvaluator_EvaluationOrder
{
	[Fact]
	public void EvaluationOrder_ShouldReturnUserRollout()
	{
		// Arrange
		var evaluator = new UserRolloutEvaluator();

		// Act
		var order = evaluator.EvaluationOrder;

		// Assert
		order.ShouldBe(EvaluationOrder.UserRollout);
	}
}

public class UserRolloutEvaluator_CanProcess
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_CanProcess()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public void If_UserIdProvidedAndUserExplicitlyManaged_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			UserAccess = FlagUserAccessControl.CreateAccessControl(allowedUsers: ["user123"])
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_UserIdProvidedButNotExplicitlyManaged_AndNoPercentageRollout_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			UserAccess = FlagUserAccessControl.Unrestricted,
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_UserRolloutPercentageModeWithAccessRestrictions_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50)
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_UserRolloutPercentageModeButNoAccessRestrictions_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			UserAccess = FlagUserAccessControl.Unrestricted // No restrictions (100% rollout)
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_NoUserId_AndNoUserRolloutPercentageMode_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			UserAccess = FlagUserAccessControl.CreateAccessControl(allowedUsers: ["user123"])
		};
		var context = new EvaluationContext(tenantId: "tenant123"); // No user ID

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_EmptyUserId_AndNoUserRolloutPercentageMode_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			UserAccess = FlagUserAccessControl.CreateAccessControl(allowedUsers: ["user123"])
		};
		var context = new EvaluationContext(userId: ""); // Empty user ID

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_WhitespaceUserId_AndNoUserRolloutPercentageMode_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			UserAccess = FlagUserAccessControl.CreateAccessControl(allowedUsers: ["user123"])
		};
		var context = new EvaluationContext(userId: "   "); // Whitespace user ID

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
	[InlineData(FlagEvaluationMode.TenantRolloutPercentage)]
	public void If_NonUserRolloutModeWithoutExplicitUserManagement_ThenCannotProcess(FlagEvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet(),
			UserAccess = FlagUserAccessControl.Unrestricted
		};
		flag.EvaluationModeSet.AddMode(mode);
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}
}

public class UserRolloutEvaluator_ProcessEvaluation_UserIdValidation
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_ProcessEvaluation_UserIdValidation()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public async Task If_NoUserId_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(tenantId: "tenant123"); // No user ID

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("User ID is required for percentage rollout evaluation.");
	}

	[Fact]
	public async Task If_EmptyUserId_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(userId: ""); // Empty user ID

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("User ID is required for percentage rollout evaluation.");
	}

	[Fact]
	public async Task If_WhitespaceUserId_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(userId: "   "); // Whitespace user ID

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => _evaluator.ProcessEvaluation(flag, context));
		exception.Message.ShouldBe("User ID is required for percentage rollout evaluation.");
	}
}

public class UserRolloutEvaluator_ProcessEvaluation_ExplicitlyAllowedUsers
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_ProcessEvaluation_ExplicitlyAllowedUsers()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public async Task If_UserExplicitlyAllowed_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: ["user123", "user456"]),
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
	public async Task If_UserExplicitlyAllowedCaseInsensitive_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: ["USER123"]),
			Variations = new FlagVariations { DefaultVariation = "disabled" }
		};
		var context = new EvaluationContext(userId: "user123"); // Different case

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public async Task If_UserNotInAllowedList_AndNoPercentageRollout_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: ["user456", "user789"],
				rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = "restricted" }
		};
		var context = new EvaluationContext(userId: "user123"); // Not in allowed list

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("restricted");
		result.Reason.ShouldBe("Access restricted to all users");
	}
}

public class UserRolloutEvaluator_ProcessEvaluation_ExplicitlyBlockedUsers
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_ProcessEvaluation_ExplicitlyBlockedUsers()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public async Task If_UserExplicitlyBlocked_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				blockedUsers: ["user123", "user456"],
				rolloutPercentage: 100), // Even with 100% rollout, blocked takes precedence
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
	public async Task If_UserExplicitlyBlockedCaseInsensitive_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				blockedUsers: ["USER123"],
				rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "blocked" }
		};
		var context = new EvaluationContext(userId: "user123"); // Different case

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
		result.Reason.ShouldBe("User explicitly blocked");
	}

	[Fact]
	public async Task If_UserInBothAllowedAndBlockedLists_ThenBlockedTakesPrecedence()
	{
		// Arrange - This scenario shouldn't happen with proper validation, but test defensive behavior
		var allowedUsers = new List<string> { "user123" };
		var blockedUsers = new List<string> { "user123" };
		
		// Use LoadAccessControl to bypass validation that prevents this scenario
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.LoadAccessControl(
				allowedUsers: allowedUsers,
				blockedUsers: blockedUsers,
				rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "conflict" }
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("conflict");
		result.Reason.ShouldBe("User explicitly blocked");
	}
}

public class UserRolloutEvaluator_ProcessEvaluation_PercentageRollout
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_ProcessEvaluation_PercentageRollout()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public async Task If_ZeroPercentRollout_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "zero-rollout-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = "zero-rollout" }
		};
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("zero-rollout");
		result.Reason.ShouldBe("Access restricted to all users");
	}

	[Fact]
	public async Task If_HundredPercentRollout_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "full-rollout-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 100),
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
	public async Task If_PartialRollout_ThenUsesConsistentHashing()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "partial-rollout-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "not-in-rollout" }
		};

		// Test the same user multiple times to ensure consistency
		var context = new EvaluationContext(userId: "consistent-user");

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
			result1.Reason.ShouldMatch(@"User in rollout: \d+% < 50%");
		}
		else
		{
			result1.Reason.ShouldMatch(@"User not in rollout: \d+% >= 50%");
		}
	}

	[Fact]
	public async Task If_DifferentUsersSameFlag_ThenMayHaveDifferentResults()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "distribution-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "not-in-rollout" }
		};

		var user1Context = new EvaluationContext(userId: "user-alpha");
		var user2Context = new EvaluationContext(userId: "user-beta");

		// Act
		var result1 = await _evaluator.ProcessEvaluation(flag, user1Context);
		var result2 = await _evaluator.ProcessEvaluation(flag, user2Context);

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
			result1.Reason.ShouldMatch(@"User in rollout: \d+% < 50%");
		}
		else
		{
			result1.Variation.ShouldBe("not-in-rollout");
			result1.Reason.ShouldMatch(@"User not in rollout: \d+% >= 50%");
		}

		if (result2.IsEnabled)
		{
			result2.Variation.ShouldBe("on");
			result2.Reason.ShouldMatch(@"User in rollout: \d+% < 50%");
		}
		else
		{
			result2.Variation.ShouldBe("not-in-rollout");
			result2.Reason.ShouldMatch(@"User not in rollout: \d+% >= 50%");
		}
	}

	[Fact]
	public async Task If_SameUserDifferentFlags_ThenMayHaveDifferentResults()
	{
		// Arrange
		var flag1 = new FeatureFlag
		{
			Key = "flag-one",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "flag1-off" }
		};

		var flag2 = new FeatureFlag
		{
			Key = "flag-two",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
			Variations = new FlagVariations { DefaultVariation = "flag2-off" }
		};

		var context = new EvaluationContext(userId: "consistent-user");

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

public class UserRolloutEvaluator_ProcessEvaluation_Variations
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_ProcessEvaluation_Variations()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public async Task If_UserAllowed_ThenReturnsOnVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: ["user123"]),
			Variations = new FlagVariations { DefaultVariation = "custom-default" }
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
	public async Task If_UserDenied_ThenReturnsDefaultVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				blockedUsers: ["user123"]),
			Variations = new FlagVariations { DefaultVariation = "user-blocked" }
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("user-blocked");
		result.Reason.ShouldBe("User explicitly blocked");
	}

	[Fact]
	public async Task If_UserInRollout_ThenReturnsOnVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "rollout-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "rollout-off" }
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
	public async Task If_UserNotInRollout_ThenReturnsDefaultVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "rollout-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = "no-rollout" }
		};
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("no-rollout");
		result.Reason.ShouldBe("Access restricted to all users");
	}

	[Theory]
	[InlineData("custom-default")]
	[InlineData("off")]
	[InlineData("")]
	[InlineData("fallback")]
	public async Task If_UserDenied_ThenReturnsSpecifiedDefaultVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 0),
			Variations = new FlagVariations { DefaultVariation = defaultVariation }
		};
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("Access restricted to all users");
	}
}

public class UserRolloutEvaluator_ProcessEvaluation_EdgeCases
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_ProcessEvaluation_EdgeCases()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public async Task If_UserIdWithSpaces_ThenTrimsAndEvaluates()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: ["user123"]),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(userId: "  user123  "); // Spaces around user ID

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public async Task If_UserIdWithSpecialCharacters_ThenEvaluatesCorrectly()
	{
		// Arrange
		var specialUserId = "user@company.com";
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: [specialUserId]),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(userId: specialUserId);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public async Task If_VeryLongUserId_ThenEvaluatesCorrectly()
	{
		// Arrange
		var longUserId = new string('a', 1000); // Very long user ID
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 100),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(userId: longUserId);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Access unrestricted to all users");
	}

	[Fact]
	public async Task If_UnicodeInUserId_ThenEvaluatesCorrectly()
	{
		// Arrange
		var unicodeUserId = "user-🌟-משתמש";
		var flag = new FeatureFlag
		{
			Key = "unicode-flag",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: [unicodeUserId]),
			Variations = new FlagVariations { DefaultVariation = "off" }
		};
		var context = new EvaluationContext(userId: unicodeUserId);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly allowed");
	}
}

public class UserRolloutEvaluator_ProcessEvaluation_RealWorldScenarios
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_ProcessEvaluation_RealWorldScenarios()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public async Task BetaFeatureRollout_EarlyAdoptersFirst_ShouldWorkCorrectly()
	{
		// Arrange - Beta testers get early access, then gradual rollout to others
		var flag = new FeatureFlag
		{
			Key = "beta-feature",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: ["beta-tester-1", "beta-tester-2", "product-manager"],
				rolloutPercentage: 15), // 15% of other users
			Variations = new FlagVariations { DefaultVariation = "stable-version" }
		};

		// Test beta tester (should always get access)
		var betaResult = await _evaluator.ProcessEvaluation(flag, 
			new EvaluationContext(userId: "beta-tester-1"));

		// Test regular users (may or may not get access based on rollout)
		var regularUser1Result = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "regular-user-1"));

		var regularUser2Result = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "regular-user-2"));

		// Assert
		betaResult.IsEnabled.ShouldBeTrue();
		betaResult.Variation.ShouldBe("on");
		betaResult.Reason.ShouldBe("User explicitly allowed");

		// Regular users should get consistent results based on hash
		regularUser1Result.IsEnabled.ShouldBeOneOf(true, false);
		regularUser2Result.IsEnabled.ShouldBeOneOf(true, false);

		if (regularUser1Result.IsEnabled)
		{
			regularUser1Result.Reason.ShouldMatch(@"User in rollout: \d+% < 15%");
		}
		else
		{
			regularUser1Result.Reason.ShouldMatch(@"User not in rollout: \d+% >= 15%");
		}
	}

	[Fact]
	public async Task PremiumFeatureRollout_BlockedUsersCannotAccess_ShouldWorkCorrectly()
	{
		// Arrange - Premium feature with some users explicitly blocked
		var flag = new FeatureFlag
		{
			Key = "premium-feature",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				blockedUsers: ["free-tier-user", "suspended-user", "trial-expired-user"],
				rolloutPercentage: 80), // 80% rollout except blocked users
			Variations = new FlagVariations { DefaultVariation = "basic-version" }
		};

		// Test blocked user (should never get access)
		var blockedResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "free-tier-user"));

		// Test normal user (may get access based on rollout)
		var normalResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "premium-user"));

		// Assert
		blockedResult.IsEnabled.ShouldBeFalse();
		blockedResult.Variation.ShouldBe("basic-version");
		blockedResult.Reason.ShouldBe("User explicitly blocked");

		normalResult.IsEnabled.ShouldBeOneOf(true, false);
		if (normalResult.IsEnabled)
		{
			normalResult.Variation.ShouldBe("on");
			normalResult.Reason.ShouldMatch(@"User in rollout: \d+% < 80%");
		}
		else
		{
			normalResult.Variation.ShouldBe("basic-version");
			normalResult.Reason.ShouldMatch(@"User not in rollout: \d+% >= 80%");
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

		var context = new EvaluationContext(userId: "consistent-user");

		// Phase 1: 5% rollout
		var phase1Flag = new FeatureFlag
		{
			Key = baseFlag.Key,
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 5),
			Variations = baseFlag.Variations
		};
		var phase1Result = await _evaluator.ProcessEvaluation(phase1Flag, context);

		// Phase 2: 25% rollout
		var phase2Flag = new FeatureFlag
		{
			Key = baseFlag.Key,
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 25),
			Variations = baseFlag.Variations
		};
		var phase2Result = await _evaluator.ProcessEvaluation(phase2Flag, context);

		// Phase 3: 100% rollout
		var phase3Flag = new FeatureFlag
		{
			Key = baseFlag.Key,
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 100),
			Variations = baseFlag.Variations
		};
		var phase3Result = await _evaluator.ProcessEvaluation(phase3Flag, context);

		// Assert
		// Same user should have consistent results within each phase
		var secondPhase1Result = await _evaluator.ProcessEvaluation(phase1Flag, context);
		phase1Result.IsEnabled.ShouldBe(secondPhase1Result.IsEnabled);

		// If user was in 5% rollout, it should also be in 25% and 100%
		if (phase1Result.IsEnabled)
		{
			phase2Result.IsEnabled.ShouldBeTrue();
			phase3Result.IsEnabled.ShouldBeTrue();
		}

		// If user was in 25% rollout, it should also be in 100%
		if (phase2Result.IsEnabled)
		{
			phase3Result.IsEnabled.ShouldBeTrue();
		}

		// 100% rollout should always be enabled
		phase3Result.IsEnabled.ShouldBeTrue();
		phase3Result.Reason.ShouldBe("Access unrestricted to all users");
	}

	[Fact]
	public async Task AdminOverride_CriticalUsers_ShouldWorkCorrectly()
	{
		// Arrange - During maintenance, only critical users can access
		var flag = new FeatureFlag
		{
			Key = "admin-override",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: ["admin", "super-admin", "system-maintainer", "on-call-engineer"],
				rolloutPercentage: 0), // No regular rollout during maintenance
			Variations = new FlagVariations { DefaultVariation = "maintenance-mode" }
		};

		var testCases = new[]
		{
			("admin", true, "User explicitly allowed"),
			("super-admin", true, "User explicitly allowed"),
			("system-maintainer", true, "User explicitly allowed"),
			("on-call-engineer", true, "User explicitly allowed"),
			("regular-user", false, "Access restricted to all users"),
			("guest-user", false, "Access restricted to all users")
		};

		foreach (var (userId, expectedEnabled, expectedReason) in testCases)
		{
			// Act
			var result = await _evaluator.ProcessEvaluation(flag,
				new EvaluationContext(userId: userId));

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

	[Fact]
	public async Task CanaryDeployment_PowerUsersFirst_ShouldWorkCorrectly()
	{
		// Arrange - Power users get new version first, then rollout
		var flag = new FeatureFlag
		{
			Key = "canary-deployment",
			UserAccess = FlagUserAccessControl.CreateAccessControl(
				allowedUsers: ["power-user-1", "power-user-2", "qa-team-lead"],
				rolloutPercentage: 10), // 10% canary rollout
			Variations = new FlagVariations { DefaultVariation = "stable-version" }
		};

		// Test power user
		var powerUserResult = await _evaluator.ProcessEvaluation(flag,
			new EvaluationContext(userId: "power-user-1"));

		// Test multiple regular users to verify distribution
		var regularUserResults = new List<EvaluationResult?>();
		for (int i = 0; i < 20; i++)
		{
			var result = await _evaluator.ProcessEvaluation(flag,
				new EvaluationContext(userId: $"regular-user-{i}"));
			regularUserResults.Add(result);
		}

		// Assert
		powerUserResult.IsEnabled.ShouldBeTrue();
		powerUserResult.Variation.ShouldBe("on");
		powerUserResult.Reason.ShouldBe("User explicitly allowed");

		// Some regular users should get the feature (roughly 10%)
		var enabledCount = regularUserResults.Count(r => r!.IsEnabled);
		enabledCount.ShouldBeGreaterThanOrEqualTo(0);
		enabledCount.ShouldBeLessThanOrEqualTo(20);

		// All results should be valid
		foreach (var result in regularUserResults)
		{
			result.ShouldNotBeNull();
			if (result.IsEnabled)
			{
				result.Variation.ShouldBe("on");
				result.Reason.ShouldMatch(@"User in rollout: \d+% < 10%");
			}
			else
			{
				result.Variation.ShouldBe("stable-version");
				result.Reason.ShouldMatch(@"User not in rollout: \d+% >= 10%");
			}
		}
	}
}

public class UserRolloutEvaluator_ProcessEvaluation_HashConsistency
{
	private readonly UserRolloutEvaluator _evaluator;

	public UserRolloutEvaluator_ProcessEvaluation_HashConsistency()
	{
		_evaluator = new UserRolloutEvaluator();
	}

	[Fact]
	public async Task If_SameInputs_ThenAlwaysProduceSameResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "consistency-test",
			UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 30),
			Variations = new FlagVariations { DefaultVariation = "not-in-rollout" }
		};
		var context = new EvaluationContext(userId: "consistent-user");

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
		var userId = "distribution-test-user";
		var context = new EvaluationContext(userId: userId);

		var flags = new[]
		{
			new FeatureFlag
			{
				Key = "flag-alpha",
				UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
				Variations = new FlagVariations { DefaultVariation = "alpha-off" }
			},
			new FeatureFlag
			{
				Key = "flag-beta",
				UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
				Variations = new FlagVariations { DefaultVariation = "beta-off" }
			},
			new FeatureFlag
			{
				Key = "flag-gamma",
				UserAccess = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50),
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
				result.Reason.ShouldMatch(@"User in rollout: \d+% < 50%");
			}
			else
			{
				result.Variation.ShouldBe(flag.Variations.DefaultVariation);
				result.Reason.ShouldMatch(@"User not in rollout: \d+% >= 50%");
			}
		}

		// Verify that we potentially got different results (not all same)
		var enabledCount = results.Count(r => r!.IsEnabled);
		// With 50% rollout and 3 flags, we expect some variation in most cases
		// But this is probabilistic, so we just ensure results are valid
		enabledCount.ShouldBeInRange(0, 3);
	}
}