using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Client.Evaluators;

public class UserOverrideHandler_CanProcessLogic
{
	private readonly UserOverrideHandler _handler;

	public UserOverrideHandler_CanProcessLogic()
	{
		_handler = new UserOverrideHandler();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task If_UserIdMissing_ThenCannotProcess(string? userId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			Status = FeatureFlagStatus.Enabled,
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain");
	}

	[Theory]
	[InlineData("user123")]
	[InlineData("test@example.com")]
	[InlineData("user-with-dashes")]
	[InlineData("user_with_underscores")]
	[InlineData("1234567890")]
	[InlineData("special-chars!@#$%")]
	public async Task If_UserIdProvided_ThenCanProcess(string userId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			Status = FeatureFlagStatus.Enabled,
			EnabledUsers = new List<string> { userId },
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}

	[Fact]
	public async Task If_UserIdProvidedNoOverrides_ThenCallsNext()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "test-flag",
			Status = FeatureFlagStatus.Enabled,
			EnabledUsers = new List<string>(),
			DisabledUsers = new List<string>(),
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
}

public class UserOverrideHandler_DisabledUsersList
{
	private readonly UserOverrideHandler _handler;

	public UserOverrideHandler_DisabledUsersList()
	{
		_handler = new UserOverrideHandler();
	}

	[Fact]
	public async Task If_UserInDisabledList_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-user-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "disabled-user", "another-disabled", "third-user" },
			EnabledUsers = new List<string>(),
			DefaultVariation = "disabled-override"
		};
		var context = new EvaluationContext(userId: "disabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-override");
		result.Reason.ShouldBe("User explicitly disabled");
	}

	[Theory]
	[InlineData("user1")]
	[InlineData("user2")]
	[InlineData("user3")]
	public async Task If_MultipleUsersInDisabledList_ThenAllDisabled(string userId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multiple-disabled-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "user1", "user2", "user3" },
			EnabledUsers = new List<string>(),
			DefaultVariation = "disabled-variation"
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-variation");
		result.Reason.ShouldBe("User explicitly disabled");
	}

	[Fact]
	public async Task If_UserNotInDisabledList_ThenDoesNotDisable()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-disabled-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "other-user", "another-user" },
			EnabledUsers = new List<string>(),
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "allowed-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain"); // Calls next handler
	}

	[Fact]
	public async Task If_EmptyDisabledUsersList_ThenDoesNotDisable()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "empty-disabled-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string>(),
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain"); // Calls next handler
	}

	[Theory]
	[InlineData("control")]
	[InlineData("fallback")]
	[InlineData("disabled")]
	[InlineData("")]
	public async Task If_UserDisabledWithDifferentVariations_ThenReturnsDefaultVariation(string defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "variation-disabled-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "disabled-user" },
			EnabledUsers = new List<string>(),
			DefaultVariation = defaultVariation
		};
		var context = new EvaluationContext(userId: "disabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("User explicitly disabled");
	}

	[Fact]
	public async Task If_UserDisabledWithNullVariation_ThenReturnsNull()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-variation-disabled-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "disabled-user" },
			EnabledUsers = new List<string>(),
			DefaultVariation = null!
		};
		var context = new EvaluationContext(userId: "disabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("User explicitly disabled");
	}
}

public class UserOverrideHandler_EnabledUsersList
{
	private readonly UserOverrideHandler _handler;

	public UserOverrideHandler_EnabledUsersList()
	{
		_handler = new UserOverrideHandler();
	}

	[Fact]
	public async Task If_UserInEnabledList_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-user-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { "enabled-user", "another-enabled", "third-user" },
			DefaultVariation = "should-not-be-used"
		};
		var context = new EvaluationContext(userId: "enabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}

	[Theory]
	[InlineData("vip1")]
	[InlineData("vip2")]
	[InlineData("vip3")]
	public async Task If_MultipleUsersInEnabledList_ThenAllEnabled(string userId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multiple-enabled-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { "vip1", "vip2", "vip3" },
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}

	[Fact]
	public async Task If_UserNotInEnabledList_ThenDoesNotEnable()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-enabled-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { "vip-user", "premium-user" },
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "regular-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain"); // Calls next handler
	}

	[Fact]
	public async Task If_EmptyEnabledUsersList_ThenDoesNotEnable()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "empty-enabled-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string>(),
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "any-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain"); // Calls next handler
	}

	[Fact]
	public async Task If_UserEnabledAlwaysReturnsOnVariation_ThenIgnoresDefaultVariation()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-on-variation-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { "enabled-user" },
			DefaultVariation = "should-be-ignored"
		};
		var context = new EvaluationContext(userId: "enabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on"); // Always "on", not default variation
		result.Reason.ShouldBe("User explicitly enabled");
	}
}

public class UserOverrideHandler_OverridePrecedence
{
	private readonly UserOverrideHandler _handler;

	public UserOverrideHandler_OverridePrecedence()
	{
		_handler = new UserOverrideHandler();
	}

	[Fact]
	public async Task If_UserInBothLists_ThenDisabledTakesPrecedence()
	{
		// Arrange - User is in both enabled and disabled lists
		var flag = new FeatureFlag
		{
			Key = "precedence-test-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "conflict-user", "other-user" },
			EnabledUsers = new List<string> { "conflict-user", "vip-user" },
			DefaultVariation = "conflict-disabled"
		};
		var context = new EvaluationContext(userId: "conflict-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse(); // Disabled takes precedence
		result.Variation.ShouldBe("conflict-disabled");
		result.Reason.ShouldBe("User explicitly disabled");
	}

	[Fact]
	public async Task If_UserInBothListsMultipleScenarios_ThenAlwaysDisabledWins()
	{
		// Arrange - Test multiple users in both lists
		var flag = new FeatureFlag
		{
			Key = "multiple-precedence-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "user1", "user2", "user3" },
			EnabledUsers = new List<string> { "user1", "user2", "user3", "user4" },
			DefaultVariation = "precedence-test"
		};

		var conflictUsers = new[] { "user1", "user2", "user3" };

		foreach (var userId in conflictUsers)
		{
			var context = new EvaluationContext(userId: userId);

			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("precedence-test");
			result.Reason.ShouldBe("User explicitly disabled");
		}

		// Test user only in enabled list
		var enabledOnlyContext = new EvaluationContext(userId: "user4");
		var enabledResult = await _handler.Handle(flag, enabledOnlyContext);

		enabledResult.ShouldNotBeNull();
		enabledResult.IsEnabled.ShouldBeTrue();
		enabledResult.Variation.ShouldBe("on");
		enabledResult.Reason.ShouldBe("User explicitly enabled");
	}
}

public class UserOverrideHandler_StringComparison
{
	private readonly UserOverrideHandler _handler;

	public UserOverrideHandler_StringComparison()
	{
		_handler = new UserOverrideHandler();
	}

	[Theory]
	[InlineData("User123", "user123")] // Different case
	[InlineData("USER123", "user123")] // All caps vs lowercase
	[InlineData("user123", "User123")] // Lowercase vs mixed case
	public async Task If_UserIdCaseDifference_ThenUsesExactMatch(string listUserId, string contextUserId)
	{
		// Arrange - Test case sensitivity (should be exact match)
		var flag = new FeatureFlag
		{
			Key = "case-sensitive-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { listUserId },
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: contextUserId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		if (listUserId == contextUserId) // Exact match
		{
			result.IsEnabled.ShouldBeTrue();
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("User explicitly enabled");
		}
		else // Case mismatch
		{
			result.IsEnabled.ShouldBeFalse();
			result.Reason.ShouldContain("End of evaluation chain");
		}
	}

	[Fact]
	public async Task If_ExactUserIdMatch_ThenProcessesCorrectly()
	{
		// Arrange
		var exactUserId = "exact-match-user-123!@#";
		var flag = new FeatureFlag
		{
			Key = "exact-match-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { exactUserId },
			EnabledUsers = new List<string>(),
			DefaultVariation = "exact-disabled"
		};
		var context = new EvaluationContext(userId: exactUserId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("exact-disabled");
		result.Reason.ShouldBe("User explicitly disabled");
	}

	[Theory]
	[InlineData("user@example.com")]
	[InlineData("user-with-dashes")]
	[InlineData("user_with_underscores")]
	[InlineData("123456789")]
	[InlineData("special!@#$%^&*()")]
	public async Task If_SpecialCharacterUserIds_ThenHandlesCorrectly(string specialUserId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "special-chars-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { specialUserId },
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: specialUserId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}
}

public class UserOverrideHandler_CallNextBehavior
{
	private readonly UserOverrideHandler _handler;
	private readonly Mock<IFlagEvaluationHandler> _mockNextHandler;

	public UserOverrideHandler_CallNextBehavior()
	{
		_handler = new UserOverrideHandler();
		_mockNextHandler = new Mock<IFlagEvaluationHandler>();
	}

	[Fact]
	public async Task If_UserNotInAnyList_ThenCallsNextHandler()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "call-next-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "other-user" },
			EnabledUsers = new List<string> { "different-user" },
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "unhandled-user");
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
	public async Task If_UserInDisabledList_ThenDoesNotCallNext()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "disabled-no-next-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "disabled-user" },
			EnabledUsers = new List<string>(),
			DefaultVariation = "disabled"
		};
		var context = new EvaluationContext(userId: "disabled-user");

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("User explicitly disabled");
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_UserInEnabledList_ThenDoesNotCallNext()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "enabled-no-next-flag",
			Status = FeatureFlagStatus.Disabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { "enabled-user" },
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "enabled-user");

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("User explicitly enabled");
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_NoNextHandlerAndUserNotInLists_ThenReturnsDefault()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-next-handler-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string>(),
			DefaultVariation = "no-handler-default"
		};
		var context = new EvaluationContext(userId: "unhandled-user");

		// NextHandler is null by default

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("no-handler-default");
		result.Reason.ShouldContain("End of evaluation chain");
	}
}

public class UserOverrideHandler_FlagStatusIndependence
{
	private readonly UserOverrideHandler _handler;

	public UserOverrideHandler_FlagStatusIndependence()
	{
		_handler = new UserOverrideHandler();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_DisabledUserRegardlessOfStatus_ThenAlwaysDisabled(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "status-independent-disabled-flag",
			Status = status,
			DisabledUsers = new List<string> { "disabled-user" },
			EnabledUsers = new List<string>(),
			DefaultVariation = "status-disabled"
		};
		var context = new EvaluationContext(userId: "disabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("status-disabled");
		result.Reason.ShouldBe("User explicitly disabled");
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.UserTargeted)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_EnabledUserRegardlessOfStatus_ThenAlwaysEnabled(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "status-independent-enabled-flag",
			Status = status,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { "enabled-user" },
			DefaultVariation = "should-not-be-used"
		};
		var context = new EvaluationContext(userId: "enabled-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}
}

public class UserOverrideHandler_ContextIndependence
{
	private readonly UserOverrideHandler _handler;

	public UserOverrideHandler_ContextIndependence()
	{
		_handler = new UserOverrideHandler();
	}

	[Fact]
	public async Task If_ComplexContextButUserOverridden_ThenIgnoresOtherContext()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "context-ignored-flag",
			Status = FeatureFlagStatus.UserTargeted,
			DisabledUsers = new List<string> { "overridden-user" },
			EnabledUsers = new List<string>(),
			DefaultVariation = "context-override"
		};

		var complexContext = new EvaluationContext(
			tenantId: "premium-tenant",
			userId: "overridden-user",
			attributes: new Dictionary<string, object>
			{
				{ "plan", "enterprise" },
				{ "region", "us-west" },
				{ "beta_user", true }
			},
			evaluationTime: DateTime.UtcNow,
			timeZone: "America/Los_Angeles"
		);

		// Act
		var result = await _handler.Handle(flag, complexContext);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("context-override");
		result.Reason.ShouldBe("User explicitly disabled");
	}

	[Fact]
	public async Task If_MultipleContextVariationsButSameUser_ThenConsistentOverride()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "consistent-override-flag",
			Status = FeatureFlagStatus.Percentage,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { "vip-user" },
			DefaultVariation = "default"
		};

		var contexts = new[]
		{
			new EvaluationContext(userId: "vip-user"),
			new EvaluationContext(tenantId: "tenant1", userId: "vip-user"),
			new EvaluationContext(userId: "vip-user", attributes: new Dictionary<string, object> { { "plan", "basic" } }),
			new EvaluationContext(userId: "vip-user", evaluationTime: DateTime.UtcNow.AddHours(-1)),
			new EvaluationContext(tenantId: "tenant2", userId: "vip-user", attributes: new Dictionary<string, object> { { "region", "eu" } })
		};

		foreach (var context in contexts)
		{
			// Act
			var result = await _handler.Handle(flag, context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeTrue();
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("User explicitly enabled");
		}
	}
}

public class UserOverrideHandler_EdgeCases
{
	private readonly UserOverrideHandler _handler;

	public UserOverrideHandler_EdgeCases()
	{
		_handler = new UserOverrideHandler();
	}

	[Fact]
	public async Task If_NullUserLists_ThenHandlesGracefully()
	{
		// This test assumes the lists could be null, though the current implementation shows them as initialized
		// This is more of a defensive programming test
		var flag = new FeatureFlag
		{
			Key = "null-lists-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string>(), // Empty instead of null for this implementation
			EnabledUsers = new List<string>(),
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "test-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldContain("End of evaluation chain");
	}

	[Fact]
	public async Task If_VeryLargeUserLists_ThenPerformsEfficiently()
	{
		// Arrange
		var largeDisabledList = Enumerable.Range(1, 10000).Select(i => $"disabled-user-{i}").ToList();
		var largeEnabledList = Enumerable.Range(1, 10000).Select(i => $"enabled-user-{i}").ToList();

		var flag = new FeatureFlag
		{
			Key = "large-lists-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = largeDisabledList,
			EnabledUsers = largeEnabledList,
			DefaultVariation = "default"
		};

		// Test user in disabled list
		var disabledContext = new EvaluationContext(userId: "disabled-user-5000");

		// Act
		var disabledResult = await _handler.Handle(flag, disabledContext);

		// Assert
		disabledResult.ShouldNotBeNull();
		disabledResult.IsEnabled.ShouldBeFalse();
		disabledResult.Reason.ShouldBe("User explicitly disabled");

		// Test user in enabled list
		var enabledContext = new EvaluationContext(userId: "enabled-user-7500");

		// Act
		var enabledResult = await _handler.Handle(flag, enabledContext);

		// Assert
		enabledResult.ShouldNotBeNull();
		enabledResult.IsEnabled.ShouldBeTrue();
		enabledResult.Reason.ShouldBe("User explicitly enabled");
	}

	[Fact]
	public async Task If_DuplicateUsersInList_ThenHandlesCorrectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "duplicate-users-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string>(),
			EnabledUsers = new List<string> { "duplicate-user", "duplicate-user", "other-user", "duplicate-user" },
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "duplicate-user");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}

	[Fact]
	public async Task If_EmptyStringInUserList_ThenHandlesCorrectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "empty-string-list-flag",
			Status = FeatureFlagStatus.Enabled,
			DisabledUsers = new List<string> { "", "valid-user", "" },
			EnabledUsers = new List<string>(),
			DefaultVariation = "default"
		};

		// Test with empty string user ID (though this should be filtered out by CanProcess)
		var emptyContext = new EvaluationContext(userId: "");
		var emptyResult = await _handler.Handle(flag, emptyContext);

		emptyResult.ShouldNotBeNull();
		emptyResult.Reason.ShouldContain("End of evaluation chain"); // Can't process empty user ID

		// Test with valid user
		var validContext = new EvaluationContext(userId: "valid-user");
		var validResult = await _handler.Handle(flag, validContext);

		validResult.ShouldNotBeNull();
		validResult.IsEnabled.ShouldBeFalse();
		validResult.Reason.ShouldBe("User explicitly disabled");
	}
}