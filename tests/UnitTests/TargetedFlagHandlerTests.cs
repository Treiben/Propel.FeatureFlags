using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Client.Evaluators;

public class TargetedFlagHandler_CanProcessLogic
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_CanProcessLogic()
	{
		_handler = new TargetedFlagHandler();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_FlagStatusNotUserTargeted_ThenCannotProcess(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "non-targeted-flag",
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
	public async Task If_FlagStatusIsUserTargeted_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "targeted-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user123" }, Variation = "targeted" }
			},
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("targeted");
		result.Reason.ShouldContain("Targeting rule matched");
	}
}

public class TargetedFlagHandler_AttributeEnrichment
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_AttributeEnrichment()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_TenantIdProvided_ThenEnrichesAttributes()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "tenant-targeted-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "tenantId", Operator = TargetingOperator.Equals, Values = new List<string> { "tenant123" }, Variation = "tenant-match" }
			},
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("tenant-match");
		result.Reason.ShouldContain("tenantId Equals tenant123");
	}

	[Fact]
	public async Task If_UserIdProvided_ThenEnrichesAttributes()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "user-targeted-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user456" }, Variation = "user-match" }
			},
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user456");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("user-match");
		result.Reason.ShouldContain("userId Equals user456");
	}

	[Fact]
	public async Task If_BothTenantAndUserProvided_ThenEnrichesBothAttributes()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "both-targeted-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "tenantId", Operator = TargetingOperator.Equals, Values = new List<string> { "tenant789" }, Variation = "tenant-match" },
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user789" }, Variation = "user-match" }
			},
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(tenantId: "tenant789", userId: "user789");

		// Act - tenant rule should match first
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("tenant-match");
		result.Reason.ShouldContain("tenantId Equals tenant789");
	}

	[Fact]
	public async Task If_ExistingAttributesAndContextIds_ThenMergesCorrectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "merged-attributes-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "region", Operator = TargetingOperator.Equals, Values = new List<string> { "us-west" }, Variation = "region-match" },
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user123" }, Variation = "user-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" }, { "plan", "premium" } };
		var context = new EvaluationContext(tenantId: "tenant123", userId: "user123", attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("region-match");
		result.Reason.ShouldContain("region Equals us-west");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public async Task If_EmptyOrNullTenantId_ThenDoesNotEnrichAttribute(string? tenantId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "empty-tenant-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "tenantId", Operator = TargetingOperator.Equals, Values = new List<string> { "tenant123" }, Variation = "should-not-match" }
			},
			DefaultVariation = "no-tenant"
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("no-tenant");
		result.Reason.ShouldBe("No targeting rules matched");
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public async Task If_EmptyOrNullUserId_ThenDoesNotEnrichAttribute(string? userId)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "empty-user-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user123" }, Variation = "should-not-match" }
			},
			DefaultVariation = "no-user"
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("no-user");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetedFlagHandler_EqualsOperator
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_EqualsOperator()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_EqualsOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "equals-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "plan", Operator = TargetingOperator.Equals, Values = new List<string> { "premium" }, Variation = "premium-variant" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("premium-variant");
		result.Reason.ShouldBe("Targeting rule matched: plan Equals premium");
	}

	[Fact]
	public async Task If_EqualsOperatorCaseInsensitive_ThenMatches()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "case-insensitive-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "region", Operator = TargetingOperator.Equals, Values = new List<string> { "US-WEST" }, Variation = "west-variant" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("west-variant");
	}

	[Fact]
	public async Task If_EqualsOperatorMultipleValues_ThenMatchesAny()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multiple-equals-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "tier", Operator = TargetingOperator.Equals, Values = new List<string> { "gold", "platinum", "diamond" }, Variation = "premium-tier" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "tier", "platinum" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("premium-tier");
		result.Reason.ShouldBe("Targeting rule matched: tier Equals gold,platinum,diamond");
	}

	[Fact]
	public async Task If_EqualsOperatorNoMatch_ThenContinuesToNextRule()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-match-equals-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "plan", Operator = TargetingOperator.Equals, Values = new List<string> { "premium" }, Variation = "should-not-match" },
				new() { Attribute = "region", Operator = TargetingOperator.Equals, Values = new List<string> { "us-east" }, Variation = "region-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "plan", "basic" }, { "region", "us-east" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("region-match");
	}
}

public class TargetedFlagHandler_NotEqualsOperator
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_NotEqualsOperator()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_NotEqualsOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-equals-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "plan", Operator = TargetingOperator.NotEquals, Values = new List<string> { "free" }, Variation = "paid-variant" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("paid-variant");
		result.Reason.ShouldBe("Targeting rule matched: plan NotEquals free");
	}

	[Fact]
	public async Task If_NotEqualsOperatorNoMatch_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-equals-no-match-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "plan", Operator = TargetingOperator.NotEquals, Values = new List<string> { "premium" }, Variation = "should-not-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("No targeting rules matched");
	}

	[Fact]
	public async Task If_NotEqualsOperatorMultipleValues_ThenMatchesNoneOf()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-equals-multiple-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "status", Operator = TargetingOperator.NotEquals, Values = new List<string> { "suspended", "banned", "inactive" }, Variation = "active-user" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "status", "active" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("active-user");
	}
}

public class TargetedFlagHandler_ContainsOperator
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_ContainsOperator()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_ContainsOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "contains-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "email", Operator = TargetingOperator.Contains, Values = new List<string> { "@company.com" }, Variation = "company-user" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "email", "user@company.com" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("company-user");
		result.Reason.ShouldBe("Targeting rule matched: email Contains @company.com");
	}

	[Fact]
	public async Task If_ContainsOperatorCaseInsensitive_ThenMatches()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "contains-case-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "userAgent", Operator = TargetingOperator.Contains, Values = new List<string> { "CHROME" }, Variation = "chrome-user" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "userAgent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("chrome-user");
	}

	[Fact]
	public async Task If_ContainsOperatorMultipleValues_ThenMatchesAny()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "contains-multiple-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "tags", Operator = TargetingOperator.Contains, Values = new List<string> { "beta", "alpha", "preview" }, Variation = "early-access" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "tags", "user,premium,beta,active" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("early-access");
	}
}

public class TargetedFlagHandler_NotContainsOperator
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_NotContainsOperator()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_NotContainsOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-contains-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "email", Operator = TargetingOperator.NotContains, Values = new List<string> { "@spam.com" }, Variation = "trusted-user" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "email", "user@company.com" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("trusted-user");
		result.Reason.ShouldBe("Targeting rule matched: email NotContains @spam.com");
	}

	[Fact]
	public async Task If_NotContainsOperatorNoMatch_ThenReturnsDisabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-contains-no-match-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "email", Operator = TargetingOperator.NotContains, Values = new List<string> { "@company.com" }, Variation = "should-not-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "email", "user@company.com" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetedFlagHandler_InOperator
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_InOperator()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_InOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "in-operator-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "country", Operator = TargetingOperator.In, Values = new List<string> { "US", "CA", "GB" }, Variation = "english-speaking" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "country", "US" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("english-speaking");
		result.Reason.ShouldBe("Targeting rule matched: country In US,CA,GB");
	}

	[Fact]
	public async Task If_InOperatorSameAsEquals_ThenBehavesIdentically()
	{
		// Arrange - In operator should behave the same as Equals for this implementation
		var flagIn = new FeatureFlag
		{
			Key = "in-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "role", Operator = TargetingOperator.In, Values = new List<string> { "admin", "moderator" }, Variation = "elevated-access" }
			},
			DefaultVariation = "default"
		};

		var flagEquals = new FeatureFlag
		{
			Key = "equals-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "role", Operator = TargetingOperator.Equals, Values = new List<string> { "admin", "moderator" }, Variation = "elevated-access" }
			},
			DefaultVariation = "default"
		};

		var attributes = new Dictionary<string, object> { { "role", "admin" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var resultIn = await _handler.Handle(flagIn, context);
		var resultEquals = await _handler.Handle(flagEquals, context);

		// Assert
		resultIn.ShouldNotBeNull();
		resultEquals.ShouldNotBeNull();
		resultIn.IsEnabled.ShouldBe(resultEquals.IsEnabled);
		resultIn.Variation.ShouldBe(resultEquals.Variation);
	}
}

public class TargetedFlagHandler_NotInOperator
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_NotInOperator()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_NotInOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "not-in-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "country", Operator = TargetingOperator.NotIn, Values = new List<string> { "BLOCKED1", "BLOCKED2" }, Variation = "allowed-country" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "country", "US" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("allowed-country");
		result.Reason.ShouldBe("Targeting rule matched: country NotIn BLOCKED1,BLOCKED2");
	}

	[Fact]
	public async Task If_NotInOperatorSameAsNotEquals_ThenBehavesIdentically()
	{
		// Arrange - NotIn operator should behave the same as NotEquals for this implementation
		var flagNotIn = new FeatureFlag
		{
			Key = "not-in-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "status", Operator = TargetingOperator.NotIn, Values = new List<string> { "banned", "suspended" }, Variation = "active-user" }
			},
			DefaultVariation = "default"
		};

		var flagNotEquals = new FeatureFlag
		{
			Key = "not-equals-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "status", Operator = TargetingOperator.NotEquals, Values = new List<string> { "banned", "suspended" }, Variation = "active-user" }
			},
			DefaultVariation = "default"
		};

		var attributes = new Dictionary<string, object> { { "status", "active" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var resultNotIn = await _handler.Handle(flagNotIn, context);
		var resultNotEquals = await _handler.Handle(flagNotEquals, context);

		// Assert
		resultNotIn.ShouldNotBeNull();
		resultNotEquals.ShouldNotBeNull();
		resultNotIn.IsEnabled.ShouldBe(resultNotEquals.IsEnabled);
		resultNotEquals.Variation.ShouldBe(resultNotEquals.Variation);
	}
}

public class TargetedFlagHandler_NumericOperators
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_NumericOperators()
	{
		_handler = new TargetedFlagHandler();
	}

	[Theory]
	[InlineData("25", "18", true)] // 25 > 18
	[InlineData("18", "25", false)] // 18 not > 25
	[InlineData("25.5", "25", true)] // 25.5 > 25
	[InlineData("invalid", "18", false)] // invalid number
	public async Task If_GreaterThanOperator_ThenEvaluatesNumericComparison(string attributeValue, string ruleValue, bool shouldMatch)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "greater-than-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "age", Operator = TargetingOperator.GreaterThan, Values = new List<string> { ruleValue }, Variation = "adult-content" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "age", attributeValue } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBe(shouldMatch);
		if (shouldMatch)
		{
			result.Variation.ShouldBe("adult-content");
			result.Reason.ShouldBe($"Targeting rule matched: age GreaterThan {ruleValue}");
		}
		else
		{
			result.Variation.ShouldBe("default");
			result.Reason.ShouldBe("No targeting rules matched");
		}
	}

	[Theory]
	[InlineData("15", "18", true)] // 15 < 18
	[InlineData("25", "18", false)] // 25 not < 18
	[InlineData("17.9", "18", true)] // 17.9 < 18
	[InlineData("invalid", "18", false)] // invalid number
	public async Task If_LessThanOperator_ThenEvaluatesNumericComparison(string attributeValue, string ruleValue, bool shouldMatch)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "less-than-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "age", Operator = TargetingOperator.LessThan, Values = new List<string> { ruleValue }, Variation = "minor-content" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "age", attributeValue } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBe(shouldMatch);
		if (shouldMatch)
		{
			result.Variation.ShouldBe("minor-content");
			result.Reason.ShouldBe($"Targeting rule matched: age LessThan {ruleValue}");
		}
		else
		{
			result.Variation.ShouldBe("default");
			result.Reason.ShouldBe("No targeting rules matched");
		}
	}

	[Fact]
	public async Task If_NumericOperatorWithMultipleValues_ThenMatchesAnyValue()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multiple-numeric-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "score", Operator = TargetingOperator.GreaterThan, Values = new List<string> { "80", "90", "95" }, Variation = "high-achiever" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "score", "85" } }; // 85 > 80
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("high-achiever");
	}

	[Fact]
	public async Task If_NumericOperatorWithInvalidRuleValue_ThenDoesNotMatch()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "invalid-rule-numeric-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "price", Operator = TargetingOperator.GreaterThan, Values = new List<string> { "invalid-number" }, Variation = "should-not-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "price", "100.50" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetedFlagHandler_MissingAttributes
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_MissingAttributes()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_AttributeNotPresent_ThenRuleDoesNotMatch()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "missing-attribute-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "nonexistent", Operator = TargetingOperator.Equals, Values = new List<string> { "value" }, Variation = "should-not-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "existing", "value" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("No targeting rules matched");
	}

	[Fact]
	public async Task If_AttributeValueIsNull_ThenTreatedAsEmptyString()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "null-attribute-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "nullable", Operator = TargetingOperator.Equals, Values = new List<string> { "" }, Variation = "empty-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "nullable", null! } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("empty-match");
	}

	[Fact]
	public async Task If_NoAttributes_ThenOnlyMatchesEnrichedContextAttributes()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-attributes-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user123" }, Variation = "user-match" }
			},
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123"); // No explicit attributes

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("user-match");
		result.Reason.ShouldContain("userId Equals user123");
	}
}

public class TargetedFlagHandler_RuleProcessingOrder
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_RuleProcessingOrder()
	{
		_handler = new TargetedFlagHandler();
	}

	[Fact]
	public async Task If_MultipleRulesMatch_ThenReturnsFirstMatch()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "multiple-rules-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "region", Operator = TargetingOperator.Equals, Values = new List<string> { "us-west" }, Variation = "first-match" },
				new() { Attribute = "plan", Operator = TargetingOperator.Equals, Values = new List<string> { "premium" }, Variation = "second-match" },
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user123" }, Variation = "third-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" }, { "plan", "premium" } };
		var context = new EvaluationContext(userId: "user123", attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("first-match"); // First rule should win
		result.Reason.ShouldContain("region Equals us-west");
	}

	[Fact]
	public async Task If_FirstRuleDoesNotMatchButSecondDoes_ThenReturnsSecondMatch()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "second-rule-match-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "region", Operator = TargetingOperator.Equals, Values = new List<string> { "us-east" }, Variation = "first-no-match" },
				new() { Attribute = "plan", Operator = TargetingOperator.Equals, Values = new List<string> { "premium" }, Variation = "second-match" },
				new() { Attribute = "tier", Operator = TargetingOperator.Equals, Values = new List<string> { "gold" }, Variation = "third-no-match" }
			},
			DefaultVariation = "default"
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" }, { "plan", "premium" }, { "tier", "silver" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("second-match");
		result.Reason.ShouldContain("plan Equals premium");
	}

	[Fact]
	public async Task If_NoRulesMatch_ThenReturnsDefault()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-rules-match-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "region", Operator = TargetingOperator.Equals, Values = new List<string> { "us-east" }, Variation = "east-variant" },
				new() { Attribute = "plan", Operator = TargetingOperator.Equals, Values = new List<string> { "enterprise" }, Variation = "enterprise-variant" }
			},
			DefaultVariation = "fallback"
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" }, { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("fallback");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetedFlagHandler_VariationHandling
{
	private readonly TargetedFlagHandler _handler;

	public TargetedFlagHandler_VariationHandling()
	{
		_handler = new TargetedFlagHandler();
	}

	[Theory]
	[InlineData("variant-a")]
	[InlineData("experiment-1")]
	[InlineData("on")]
	[InlineData("")]
	public async Task If_RuleMatches_ThenReturnsRuleVariation(string ruleVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "variation-test-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user123" }, Variation = ruleVariation }
			},
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe(ruleVariation);
	}

	[Theory]
	[InlineData("default-variant")]
	[InlineData("control")]
	[InlineData("")]
	public async Task If_NoRulesMatch_ThenReturnsDefaultVariation(string? defaultVariation)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "default-variation-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "plan", Operator = TargetingOperator.Equals, Values = new List<string> { "enterprise" }, Variation = "premium-features" }
			},
			DefaultVariation = defaultVariation!
		};
		var attributes = new Dictionary<string, object> { { "plan", "basic" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetedFlagHandler_ChainOfResponsibilityIntegration
{
	private readonly TargetedFlagHandler _handler;
	private readonly Mock<IFlagEvaluationHandler> _mockNextHandler;

	public TargetedFlagHandler_ChainOfResponsibilityIntegration()
	{
		_handler = new TargetedFlagHandler();
		_mockNextHandler = new Mock<IFlagEvaluationHandler>();
	}

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled)]
	[InlineData(FeatureFlagStatus.Enabled)]
	[InlineData(FeatureFlagStatus.Scheduled)]
	[InlineData(FeatureFlagStatus.TimeWindow)]
	[InlineData(FeatureFlagStatus.Percentage)]
	public async Task If_FlagStatusNotUserTargeted_ThenCallsNextHandler(FeatureFlagStatus status)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "non-targeted-flag",
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
	public async Task If_FlagStatusUserTargeted_ThenDoesNotCallNextHandler()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "targeted-flag",
			Status = FeatureFlagStatus.UserTargeted,
			TargetingRules = new List<TargetingRule>
			{
				new() { Attribute = "userId", Operator = TargetingOperator.Equals, Values = new List<string> { "user123" }, Variation = "targeted" }
			},
			DefaultVariation = "default"
		};
		var context = new EvaluationContext(userId: "user123");

		_handler.NextHandler = _mockNextHandler.Object;

		// Act
		var result = await _handler.Handle(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("targeted");
		_mockNextHandler.Verify(x => x.Handle(It.IsAny<FeatureFlag>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_NoNextHandler_ThenReturnsAppropriateResult()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			Key = "no-next-handler-flag",
			Status = FeatureFlagStatus.Enabled, // Not user targeted
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