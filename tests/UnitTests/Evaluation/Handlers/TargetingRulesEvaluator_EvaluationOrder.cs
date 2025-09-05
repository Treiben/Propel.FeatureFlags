using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.Handlers;

namespace FeatureFlags.UnitTests.Evaluation.Handlers;

public class TargetingRulesEvaluator_EvaluationOrder
{
	[Fact]
	public void EvaluationOrder_ShouldReturnCustomTargeting()
	{
		// Arrange
		var evaluator = new TargetingRulesEvaluator();

		// Act
		var order = evaluator.EvaluationOrder;

		// Assert
		order.ShouldBe(EvaluationOrder.CustomTargeting);
	}
}

public class TargetingRulesEvaluator_CanProcess
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_CanProcess()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public void If_FlagContainsUserTargetedMode_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_FlagContainsMultipleModesIncludingUserTargeted_ThenCanProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Disabled)]
	[InlineData(FlagEvaluationMode.Enabled)]
	[InlineData(FlagEvaluationMode.Scheduled)]
	[InlineData(FlagEvaluationMode.TimeWindow)]
	[InlineData(FlagEvaluationMode.UserRolloutPercentage)]
	public void If_FlagDoesNotContainUserTargetedMode_ThenCannotProcess(FlagEvaluationMode mode)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		flag.EvaluationModeSet.AddMode(mode);
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_FlagHasNoModes_ThenCannotProcess()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			EvaluationModeSet = new FlagEvaluationModeSet()
		};
		var context = new EvaluationContext();

		// Act
		var result = _evaluator.CanProcess(flag, context);

		// Assert
		result.ShouldBeFalse();
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_AttributeEnrichment
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_AttributeEnrichment()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_TenantIdProvided_ThenEnrichesAttributes()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "tenantId", 
					Operator = TargetingOperator.Equals, 
					Values = ["tenant123"], 
					Variation = "tenant-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var context = new EvaluationContext(tenantId: "tenant123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("tenant-match");
		result.Reason.ShouldBe("Targeting rule matched: tenantId Equals tenant123");
	}

	[Fact]
	public async Task If_UserIdProvided_ThenEnrichesAttributes()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "userId", 
					Operator = TargetingOperator.Equals, 
					Values = ["user456"], 
					Variation = "user-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var context = new EvaluationContext(userId: "user456");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("user-match");
		result.Reason.ShouldBe("Targeting rule matched: userId Equals user456");
	}

	[Fact]
	public async Task If_BothTenantAndUserProvided_ThenEnrichesBothAttributes()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "tenantId", 
					Operator = TargetingOperator.Equals, 
					Values = ["tenant789"], 
					Variation = "tenant-match" 
				},
				new TargetingRule 
				{ 
					Attribute = "userId", 
					Operator = TargetingOperator.Equals, 
					Values = ["user789"], 
					Variation = "user-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var context = new EvaluationContext(tenantId: "tenant789", userId: "user789");

		// Act - first rule should match
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("tenant-match");
		result.Reason.ShouldBe("Targeting rule matched: tenantId Equals tenant789");
	}

	[Fact]
	public async Task If_ExistingAttributesAndContextIds_ThenMergesCorrectly()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "region", 
					Operator = TargetingOperator.Equals, 
					Values = ["us-west"], 
					Variation = "region-match" 
				},
				new TargetingRule 
				{ 
					Attribute = "userId", 
					Operator = TargetingOperator.Equals, 
					Values = ["user123"], 
					Variation = "user-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" }, { "plan", "premium" } };
		var context = new EvaluationContext(tenantId: "tenant123", userId: "user123", attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("region-match");
		result.Reason.ShouldBe("Targeting rule matched: region Equals us-west");
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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "tenantId", 
					Operator = TargetingOperator.Equals, 
					Values = ["tenant123"], 
					Variation = "should-not-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "no-tenant" }
		};
		var context = new EvaluationContext(tenantId: tenantId);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "userId", 
					Operator = TargetingOperator.Equals, 
					Values = ["user123"], 
					Variation = "should-not-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "no-user" }
		};
		var context = new EvaluationContext(userId: userId);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("no-user");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_EqualsOperator
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_EqualsOperator()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_EqualsOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.Equals, 
					Values = ["premium"], 
					Variation = "premium-variant" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "region", 
					Operator = TargetingOperator.Equals, 
					Values = ["US-WEST"], 
					Variation = "west-variant" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "tier", 
					Operator = TargetingOperator.Equals, 
					Values = ["gold", "platinum", "diamond"], 
					Variation = "premium-tier" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "tier", "platinum" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.Equals, 
					Values = ["premium"], 
					Variation = "should-not-match" 
				},
				new TargetingRule 
				{ 
					Attribute = "region", 
					Operator = TargetingOperator.Equals, 
					Values = ["us-east"], 
					Variation = "region-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "plan", "basic" }, { "region", "us-east" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("region-match");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_NotEqualsOperator
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_NotEqualsOperator()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_NotEqualsOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.NotEquals, 
					Values = ["free"], 
					Variation = "paid-variant" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.NotEquals, 
					Values = ["premium"], 
					Variation = "should-not-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "status", 
					Operator = TargetingOperator.NotEquals, 
					Values = ["suspended", "banned", "inactive"], 
					Variation = "active-user" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "status", "active" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("active-user");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_ContainsOperator
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_ContainsOperator()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_ContainsOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "email", 
					Operator = TargetingOperator.Contains, 
					Values = ["@company.com"], 
					Variation = "company-user" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "email", "user@company.com" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "userAgent", 
					Operator = TargetingOperator.Contains, 
					Values = ["CHROME"], 
					Variation = "chrome-user" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "userAgent", "Mozilla/5.0 Chrome/91.0.4472.124" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "tags", 
					Operator = TargetingOperator.Contains, 
					Values = ["beta", "alpha", "preview"], 
					Variation = "early-access" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "tags", "user,premium,beta,active" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("early-access");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_NotContainsOperator
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_NotContainsOperator()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_NotContainsOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "email", 
					Operator = TargetingOperator.NotContains, 
					Values = ["@spam.com"], 
					Variation = "trusted-user" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "email", "user@company.com" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "email", 
					Operator = TargetingOperator.NotContains, 
					Values = ["@company.com"], 
					Variation = "should-not-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "email", "user@company.com" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("No targeting rules matched");
	}

	[Fact]
	public async Task If_NotContainsOperatorMultipleValues_ThenMatchesNoneOf()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "userAgent", 
					Operator = TargetingOperator.NotContains, 
					Values = ["bot", "crawler", "spider"], 
					Variation = "human-user" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "userAgent", "Mozilla/5.0 Chrome/91.0.4472.124" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("human-user");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_InOperator
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_InOperator()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_InOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "country", 
					Operator = TargetingOperator.In, 
					Values = ["US", "CA", "GB"], 
					Variation = "english-speaking" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "country", "US" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "role", 
					Operator = TargetingOperator.In, 
					Values = ["admin", "moderator"], 
					Variation = "elevated-access" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};

		var flagEquals = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "role", 
					Operator = TargetingOperator.Equals, 
					Values = ["admin", "moderator"], 
					Variation = "elevated-access" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};

		var attributes = new Dictionary<string, object> { { "role", "admin" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var resultIn = await _evaluator.ProcessEvaluation(flagIn, context);
		var resultEquals = await _evaluator.ProcessEvaluation(flagEquals, context);

		// Assert
		resultIn.ShouldNotBeNull();
		resultEquals.ShouldNotBeNull();
		resultIn.IsEnabled.ShouldBe(resultEquals.IsEnabled);
		resultIn.Variation.ShouldBe(resultEquals.Variation);
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_NotInOperator
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_NotInOperator()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_NotInOperatorMatches_ThenReturnsEnabled()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "country", 
					Operator = TargetingOperator.NotIn, 
					Values = ["BLOCKED1", "BLOCKED2"], 
					Variation = "allowed-country" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "country", "US" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "status", 
					Operator = TargetingOperator.NotIn, 
					Values = ["banned", "suspended"], 
					Variation = "active-user" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};

		var flagNotEquals = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "status", 
					Operator = TargetingOperator.NotEquals, 
					Values = ["banned", "suspended"], 
					Variation = "active-user" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};

		var attributes = new Dictionary<string, object> { { "status", "active" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var resultNotIn = await _evaluator.ProcessEvaluation(flagNotIn, context);
		var resultNotEquals = await _evaluator.ProcessEvaluation(flagNotEquals, context);

		// Assert
		resultNotIn.ShouldNotBeNull();
		resultNotEquals.ShouldNotBeNull();
		resultNotIn.IsEnabled.ShouldBe(resultNotEquals.IsEnabled);
		resultNotIn.Variation.ShouldBe(resultNotEquals.Variation);
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_NumericOperators
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_NumericOperators()
	{
		_evaluator = new TargetingRulesEvaluator();
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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "age", 
					Operator = TargetingOperator.GreaterThan, 
					Values = [ruleValue], 
					Variation = "adult-content" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "age", attributeValue } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "age", 
					Operator = TargetingOperator.LessThan, 
					Values = [ruleValue], 
					Variation = "minor-content" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "age", attributeValue } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "score", 
					Operator = TargetingOperator.GreaterThan, 
					Values = ["80", "90", "95"], 
					Variation = "high-achiever" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "score", "85" } }; // 85 > 80
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "price", 
					Operator = TargetingOperator.GreaterThan, 
					Values = ["invalid-number"], 
					Variation = "should-not-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "price", "100.50" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_MissingAttributes
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_MissingAttributes()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_AttributeNotPresent_ThenRuleDoesNotMatch()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "nonexistent", 
					Operator = TargetingOperator.Equals, 
					Values = ["value"], 
					Variation = "should-not-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "existing", "value" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "nullable", 
					Operator = TargetingOperator.Equals, 
					Values = [""], 
					Variation = "empty-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "nullable", null! } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "userId", 
					Operator = TargetingOperator.Equals, 
					Values = ["user123"], 
					Variation = "user-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var context = new EvaluationContext(userId: "user123"); // No explicit attributes

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("user-match");
		result.Reason.ShouldBe("Targeting rule matched: userId Equals user123");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_RuleProcessingOrder
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_RuleProcessingOrder()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_MultipleRulesMatch_ThenReturnsFirstMatch()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "region", 
					Operator = TargetingOperator.Equals, 
					Values = ["us-west"], 
					Variation = "first-match" 
				},
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.Equals, 
					Values = ["premium"], 
					Variation = "second-match" 
				},
				new TargetingRule 
				{ 
					Attribute = "userId", 
					Operator = TargetingOperator.Equals, 
					Values = ["user123"], 
					Variation = "third-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" }, { "plan", "premium" } };
		var context = new EvaluationContext(userId: "user123", attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("first-match"); // First rule should win
		result.Reason.ShouldBe("Targeting rule matched: region Equals us-west");
	}

	[Fact]
	public async Task If_FirstRuleDoesNotMatchButSecondDoes_ThenReturnsSecondMatch()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "region", 
					Operator = TargetingOperator.Equals, 
					Values = ["us-east"], 
					Variation = "first-no-match" 
				},
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.Equals, 
					Values = ["premium"], 
					Variation = "second-match" 
				},
				new TargetingRule 
				{ 
					Attribute = "tier", 
					Operator = TargetingOperator.Equals, 
					Values = ["gold"], 
					Variation = "third-no-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" }, { "plan", "premium" }, { "tier", "silver" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("second-match");
		result.Reason.ShouldBe("Targeting rule matched: plan Equals premium");
	}

	[Fact]
	public async Task If_NoRulesMatch_ThenReturnsDefault()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "region", 
					Operator = TargetingOperator.Equals, 
					Values = ["us-east"], 
					Variation = "east-variant" 
				},
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.Equals, 
					Values = ["enterprise"], 
					Variation = "enterprise-variant" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "fallback" }
		};
		var attributes = new Dictionary<string, object> { { "region", "us-west" }, { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("fallback");
		result.Reason.ShouldBe("No targeting rules matched");
	}

	[Fact]
	public async Task If_NoTargetingRules_ThenReturnsDefault()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = [],
			Variations = new FlagVariations { DefaultVariation = "no-rules" }
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("no-rules");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_VariationHandling
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_VariationHandling()
	{
		_evaluator = new TargetingRulesEvaluator();
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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "userId", 
					Operator = TargetingOperator.Equals, 
					Values = ["user123"], 
					Variation = ruleVariation 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

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
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.Equals, 
					Values = ["enterprise"], 
					Variation = "premium-features" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = defaultVariation! }
		};
		var attributes = new Dictionary<string, object> { { "plan", "basic" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(defaultVariation);
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_EdgeCases
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_EdgeCases()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task If_AttributeValueIsNonString_ThenConvertsToString()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "count", 
					Operator = TargetingOperator.Equals, 
					Values = ["42"], 
					Variation = "number-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "count", 42 } }; // Integer value
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("number-match");
	}

	[Fact]
	public async Task If_AttributeValueIsBooleanTrue_ThenConvertsToString()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "premium", 
					Operator = TargetingOperator.Equals, 
					Values = ["True"], 
					Variation = "boolean-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "premium", true } }; // Boolean value
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("boolean-match");
	}

	[Fact]
	public async Task If_EmptyRuleValues_ThenDoesNotMatch()
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = TargetingOperator.Equals, 
					Values = [], // Empty values list
					Variation = "should-not-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("No targeting rules matched");
	}

	[Theory]
	[InlineData((TargetingOperator)999)] // Invalid enum value
	public async Task If_UnsupportedOperator_ThenDoesNotMatch(TargetingOperator unsupportedOperator)
	{
		// Arrange
		var flag = new FeatureFlag
		{
			TargetingRules = 
			[
				new TargetingRule 
				{ 
					Attribute = "plan", 
					Operator = unsupportedOperator, 
					Values = ["premium"], 
					Variation = "should-not-match" 
				}
			],
			Variations = new FlagVariations { DefaultVariation = "default" }
		};
		var attributes = new Dictionary<string, object> { { "plan", "premium" } };
		var context = new EvaluationContext(attributes: attributes);

		// Act
		var result = await _evaluator.ProcessEvaluation(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("default");
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class TargetingRulesEvaluator_ProcessEvaluation_RealWorldScenarios
{
	private readonly TargetingRulesEvaluator _evaluator;

	public TargetingRulesEvaluator_ProcessEvaluation_RealWorldScenarios()
	{
		_evaluator = new TargetingRulesEvaluator();
	}

	[Fact]
	public async Task UserSegmentation_PremiumUsersWithBetaAccess_ShouldWorkCorrectly()
	{
		// Arrange - Premium users in beta program get new features
		var flag = new FeatureFlag
		{
			TargetingRules =
			[
				new TargetingRule
				{
					Attribute = "plan",
					Operator = TargetingOperator.Equals,
					Values = ["premium", "enterprise"],
					Variation = "premium-features"
				},
				new TargetingRule
				{
					Attribute = "betaAccess",
					Operator = TargetingOperator.Equals,
					Values = ["true"],
					Variation = "beta-features"
				}
			],
			Variations = new FlagVariations { DefaultVariation = "standard-features" }
		};

		// Test premium user (should get premium features)
		var premiumUser = new EvaluationContext(
			attributes: new Dictionary<string, object> { { "plan", "premium" }, { "betaAccess", "false" } });
		var premiumResult = await _evaluator.ProcessEvaluation(flag, premiumUser);

		// Test beta user (should get beta features)
		var betaUser = new EvaluationContext(
			attributes: new Dictionary<string, object> { { "plan", "basic" }, { "betaAccess", "true" } });
		var betaResult = await _evaluator.ProcessEvaluation(flag, betaUser);

		// Test standard user
		var standardUser = new EvaluationContext(
			attributes: new Dictionary<string, object> { { "plan", "basic" }, { "betaAccess", "false" } });
		var standardResult = await _evaluator.ProcessEvaluation(flag, standardUser);

		// Assert
		premiumResult.IsEnabled.ShouldBeTrue();
		premiumResult.Variation.ShouldBe("premium-features");

		betaResult.IsEnabled.ShouldBeTrue();
		betaResult.Variation.ShouldBe("beta-features");

		standardResult.IsEnabled.ShouldBeFalse();
		standardResult.Variation.ShouldBe("standard-features");
	}

	[Fact]
	public async Task GeographicTargeting_RegionSpecificFeatures_ShouldWorkCorrectly()
	{
		// Arrange - Different features for different regions
		var flag = new FeatureFlag
		{
			TargetingRules =
			[
				new TargetingRule
				{
					Attribute = "country",
					Operator = TargetingOperator.In,
					Values = ["US", "CA"],
					Variation = "north-america-features"
				},
				new TargetingRule
				{
					Attribute = "country",
					Operator = TargetingOperator.In,
					Values = ["GB", "DE", "FR"],
					Variation = "europe-features"
				},
				new TargetingRule
				{
					Attribute = "region",
					Operator = TargetingOperator.Equals,
					Values = ["asia-pacific"],
					Variation = "apac-features"
				}
			],
			Variations = new FlagVariations { DefaultVariation = "global-features" }
		};

		var testCases = new[]
		{
			(new Dictionary<string, object> { { "country", "US" } }, "north-america-features"),
			(new Dictionary<string, object> { { "country", "GB" } }, "europe-features"),
			(new Dictionary<string, object> { { "region", "asia-pacific" } }, "apac-features"),
			(new Dictionary<string, object> { { "country", "BR" } }, "global-features")
		};

		foreach (var (attributes, expectedVariation) in testCases)
		{
			// Act
			var result = await _evaluator.ProcessEvaluation(flag, new EvaluationContext(attributes: attributes));

			// Assert
			result.Variation.ShouldBe(expectedVariation);
		}
	}
}