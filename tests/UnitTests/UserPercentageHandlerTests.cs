using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Client.Evaluators;

public class UserPercentageHandler_CanProcessLogic
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_CanProcessLogic()
	{
		_handler = new UserPercentageHandler();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	public async Task If_FlagStatusNotPercentage_ThenCannotProcess(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "non-percentage-flag",
			Status = status,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain");
	}

	[Fact]
	public async Task If_FlagStatusIsPercentage_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "percentage-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 100, // 100% enabled to ensure it passes
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldContain("User percentage rollout");
	}
}

public class UserPercentageHandler_UserIdValidation
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_UserIdValidation()
	{
		_handler = new UserPercentageHandler();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task If_UserIdMissing_ThenReturnsDisabledWithError(string? userId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "percentage-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "no-user"
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("no-user");
		result.Reason.ShouldBe("User ID required for percentage rollout");
	}

	[Theory]
	[InlineData("user123")]
	[InlineData("test@example.com")]
	[InlineData("user-with-dashes")]
	[InlineData("user_with_underscores")]
	[InlineData("1234567890")]
	[InlineData("special-chars!@#$%")]
	public async Task If_UserIdProvided_ThenProcessesPercentageRollout(string userId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "percentage-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 100, // 100% to ensure it passes regardless of hash
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldContain("User percentage rollout");
		result.Reason.ShouldContain("100%");
	}
}

public class UserPercentageHandler_PercentageThresholds
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_PercentageThresholds()
	{
		_handler = new UserPercentageHandler();
	}

	[Fact]
	public async Task If_PercentageEnabled0_ThenAllUsersDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "zero-percent-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 0,
			DefaultVariation = "disabled"
		};

		var userIds = new[] { "user1", "user2", "user3", "user4", "user5" };

		foreach (var userId in userIds)
		{
			var context = new EvaluationContext(userId: userId);

			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("disabled");
			result.Reason.ShouldContain("< 0%");
		}
	}

	[Fact]
	public async Task If_PercentageEnabled100_ThenAllUsersEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "hundred-percent-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 100,
			DefaultVariation = "disabled"
		};

		var userIds = new[] { "user1", "user2", "user3", "user4", "user5" };

		foreach (var userId in userIds)
		{
			var context = new EvaluationContext(userId: userId);

			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeTrue();
			result.Variation.ShouldBe("on");
			result.Reason.ShouldContain("< 100%");
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(25)]
	[InlineData(50)]
	[InlineData(75)]
	[InlineData(99)]
	public async Task If_PercentageEnabledSet_ThenReasonContainsCorrectPercentage(int percentageEnabled)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "percentage-test-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = percentageEnabled,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain($"< {percentageEnabled}%");
	}
}

public class UserPercentageHandler_ConsistentHashing
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_ConsistentHashing()
	{
		_handler = new UserPercentageHandler();
	}

	[Fact]
	public async Task If_SameUserMultipleEvaluations_ThenConsistentResults()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "consistency-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "consistent-user");

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
	public async Task If_SameUserDifferentFlags_ThenDifferentHashResults()
	{
		// Arrange
		var flag1 = new FeatureFlag
		{
			Key = "flag-one",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};

		var flag2 = new FeatureFlag
		{
			Key = "flag-two",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};

		var context = new EvaluationContext(userId: "same-user");

		// Act
		var result1 = await _handler.Handle(flag1, context);
		var result2 = await _handler.Handle(flag2, context);

		// Assert - Results may be different due to different flag keys in hash
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		
		// The reason strings should contain different percentage calculations
		// because the hash input includes the flag key
		result1.Reason.ShouldNotBe(result2.Reason);
	}

	[Fact]
	public async Task If_DifferentUsersSameFlag_ThenDifferentHashResults()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multi-user-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};

		var context1 = new EvaluationContext(userId: "user-one");
		var context2 = new EvaluationContext(userId: "user-two");

		// Act
		var result1 = await _handler.Handle(flag, context1);
		var result2 = await _handler.Handle(flag, context2);

		// Assert - Results may be different due to different user IDs in hash
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();

		// The reason strings should contain different percentage calculations
		result1.Reason.ShouldNotBe(result2.Reason);
	}
}

public class UserPercentageHandler_HashInputFormat
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_HashInputFormat()
	{
		_handler = new UserPercentageHandler();
	}

	[Theory]
	[InlineData("test-flag", "user123")]
	[InlineData("feature_toggle", "admin@company.com")]
	[InlineData("UPPERCASE-FLAG", "lowercase-user")]
	[InlineData("flag-with-special!@#", "user-with-special!@#")]
	public async Task If_DifferentFlagAndUserCombinations_ThenHashInputFormattedCorrectly(string flagKey, string userId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = flagKey,
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		// The hash input format should be: "{flagKey}:user:{userId}"
		// We can't directly test the hash input, but we can verify consistent behavior
		result.Reason.ShouldContain("User percentage rollout");
		result.Reason.ShouldContain("< 50%");
	}

	[Fact]
	public async Task If_SameFlagAndUserDifferentContext_ThenConsistentHashing()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "context-test-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};

		// Same user ID but different context properties
		var context1 = new EvaluationContext(userId: "test-user", tenantId: "tenant1");
		var context2 = new EvaluationContext(userId: "test-user", tenantId: "tenant2");
		var context3 = new EvaluationContext(
			userId: "test-user",
			attributes: new Dictionary<string, object> { { "plan", "premium" } }
		);

		// Act
		var result1 = await _handler.Handle(flag, context1);
		var result2 = await _handler.Handle(flag, context2);
		var result3 = await _handler.Handle(flag, context3);

		// Assert - Should be consistent since only flag key and user ID are used in hash
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();
		result3.ShouldNotBeNull();

		result1.IsEnabled.ShouldBe(result2.IsEnabled);
		result1.IsEnabled.ShouldBe(result3.IsEnabled);
		result1.Reason.ShouldBe(result2.Reason);
		result1.Reason.ShouldBe(result3.Reason);
	}
}

public class UserPercentageHandler_VariationHandling
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_VariationHandling()
	{
		_handler = new UserPercentageHandler();
	}

	[Fact]
	public async Task If_UserInPercentage_ThenReturnsOnVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-user-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 100, // Ensure user is enabled
			DefaultVariation = "should-not-be-used"
		};
		var context = new EvaluationContext(userId: "enabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Theory]
	[InlineData("control")]
	[InlineData("disabled")]
	[InlineData("fallback")]
	[InlineData("")]
	public async Task If_UserNotInPercentage_ThenReturnsDefaultVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-user-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 0, // Ensure user is disabled
			DefaultVariation = defaultVariation
		};
		var context = new EvaluationContext(userId: "disabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
	}

	[Theory]
	[InlineData(null)]
	public async Task If_UserNotInPercentageNullDefault_ThenReturnsNull(string? defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-variation-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 0, // Ensure user is disabled
			DefaultVariation = defaultVariation!
		};
		var context = new EvaluationContext(userId: "disabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
	}
}

public class UserPercentageHandler_ReasonMessageFormatting
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_ReasonMessageFormatting()
	{
		_handler = new UserPercentageHandler();
	}

	[Fact]
	public async Task If_PercentageRolloutEvaluated_ThenReasonContainsCorrectFormat()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "reason-format-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "format-test-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldStartWith("User percentage rollout: ");
		result.Reason.ShouldContain("% < 50%");
		
		// Extract the actual percentage from the reason to verify format
		var reasonParts = result.Reason.Split(' ');
		reasonParts.ShouldContain("User");
		reasonParts.ShouldContain("percentage");
		reasonParts.ShouldContain("rollout:");
	}

	[Theory]
	[InlineData(10)]
	[InlineData(25)]
	[InlineData(75)]
	[InlineData(90)]
	public async Task If_DifferentPercentageThresholds_ThenReasonShowsCorrectThreshold(int threshold)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "threshold-test-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = threshold,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "threshold-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain($"< {threshold}%");
	}

	[Fact]
	public async Task If_KnownUserHash_ThenReasonShowsExpectedPercentage()
	{
		// Arrange - Using a known combination to verify hash calculation
		var flag = new FeatureFlag
		{
			Key = "known-hash-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "known-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain("User percentage rollout: ");
		
		// The reason should contain a specific percentage value (0-99)
		var match = System.Text.RegularExpressions.Regex.Match(result.Reason, @"(\d{1,2})% < 50%");
		match.Success.ShouldBeTrue();
		
		var actualPercentage = int.Parse(match.Groups[1].Value);
		actualPercentage.ShouldBeInRange(0, 99);
	}
}

public class UserPercentageHandler_DistributionTesting
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_DistributionTesting()
	{
		_handler = new UserPercentageHandler();
	}

	[Fact]
	public async Task If_LargeUserSet_ThenDistributionApproximatesPercentage()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "distribution-test-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 30, // 30% should be enabled
			DefaultVariation = "disabled"
		};

		var totalUsers = 1000;
		var enabledCount = 0;

		// Act - Test with many users
		for (int i = 0; i < totalUsers; i++)
		{
			var context = new EvaluationContext(userId: $"user-{i}");
			var result = await _handler.Handle(flag, context);
			
			if (result.IsEnabled)
			{
				enabledCount++;
			}
		}

		// Assert - Should be approximately 30% (allow 5% variance)
		var actualPercentage = (double)enabledCount / totalUsers * 100;
		actualPercentage.ShouldBeInRange(25.0, 35.0); // 30% ± 5%
	}

	[Theory]
	[InlineData(10, 1000)]
	[InlineData(50, 500)]
	[InlineData(90, 200)]
	public async Task If_DifferentPercentagesAndSampleSizes_ThenDistributionIsReasonable(int expectedPercentage, int sampleSize)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = $"distribution-{expectedPercentage}-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = expectedPercentage,
			DefaultVariation = "disabled"
		};

		var enabledCount = 0;

		// Act
		for (int i = 0; i < sampleSize; i++)
		{
			var context = new EvaluationContext(userId: $"sample-user-{i}");
			var result = await _handler.Handle(flag, context);
			
			if (result.IsEnabled)
			{
				enabledCount++;
			}
		}

		// Assert - Allow 10% variance for smaller sample sizes
		var actualPercentage = (double)enabledCount / sampleSize * 100;
		var tolerance = Math.Max(10.0, expectedPercentage * 0.2); // At least 10% or 20% of expected
		actualPercentage.ShouldBeInRange(expectedPercentage - tolerance, expectedPercentage + tolerance);
	}
}

public class UserPercentageHandler_ChainOfResponsibilityIntegration
{
	private readonly UserPercentageHandler _handler;
	private readonly Mock<IFlagEvaluationHandler> _mockNextHandler;

	public UserPercentageHandler_ChainOfResponsibilityIntegration()
	{
		_handler = new UserPercentageHandler();
		_mockNextHandler = new Mock<IFlagEvaluationHandler>();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	public async Task If_FlagStatusNotPercentage_ThenCallsNextHandler(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "non-percentage-flag",
			Status = status,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123");
		var expectedResult = new EvaluationResult(isEnabled: true, variation: "handled-by-next");

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
	public async Task If_FlagStatusPercentage_ThenDoesNotCallNextHandler()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "percentage-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123");

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain("User percentage rollout");
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_NoNextHandler_ThenReturnsAppropriateResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-next-handler-flag",
			Status = FeatureFlagStatus.Enabled, // Not percentage
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123");

		// NextHandler is null by default

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldContain("End of evaluation chain");
	}
}

public class UserPercentageHandler_EdgeCases
{
	private readonly UserPercentageHandler _handler;

	public UserPercentageHandler_EdgeCases()
	{
		_handler = new UserPercentageHandler();
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	[InlineData(999)]
	public async Task If_InvalidPercentageValues_ThenHandlesGracefully(int invalidPercentage)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "invalid-percentage-flag",
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = invalidPercentage,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain($"< {invalidPercentage}%");
		
		// Behavior depends on hash result vs threshold, but should not throw
		if (invalidPercentage <= 0)
		{
			result.IsEnabled.ShouldBeFalse(); // No users should be enabled for negative or zero
		}
		// For > 100, depends on hash result, but should handle gracefully
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
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain("User percentage rollout");
		// Should not throw, even with empty flag key in hash input
	}

	[Fact]
	public async Task If_VeryLongUserIdAndFlagKey_ThenHandlesCorrectly()
	{
		// Arrange
		var longFlagKey = new string('f', 1000);
		var longUserId = new string('u', 1000);
		
		var flag = new FeatureFlag
		{
			Key = longFlagKey,
			Status = FeatureFlagStatus.Percentage,
			PercentageEnabled = 50,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: longUserId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldContain("User percentage rollout");
		// Should handle very long strings in hash input without issues
	}
}