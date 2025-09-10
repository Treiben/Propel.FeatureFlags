using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using System.Text.Json;

namespace UnitTests.Api.Dto;

public class FeatureFlagResponseTests
{
	[Fact]
	public void Constructor_WithMixedTargetingRules_SerializesCorrectly()
	{
		// Arrange
		var flag = CreateTestFeatureFlag();
		flag.TargetingRules = new List<ITargetingRule>
		{
			new StringTargetingRule
			{
				Attribute = "userId",
				Operator = TargetingOperator.Equals,
				Values = new List<string> { "user123", "user456" },
				Variation = "premium"
			},
			new NumericTargetingRule
			{
				Attribute = "age",
				Operator = TargetingOperator.GreaterThan,
				Values = new List<double> { 18.0 },
				Variation = "adult"
			},
			new StringTargetingRule
			{
				Attribute = "plan",
				Operator = TargetingOperator.In,
				Values = new List<string> { "enterprise", "business" },
				Variation = "advanced"
			}
		};

		// Act
		var response = new FeatureFlagResponse(flag);

		// Assert
		response.TargetingRules.ShouldNotBeEmpty();

		// Verify the JSON can be deserialized back to the original rules
		var deserializedRules = JsonSerializer.Deserialize<List<ITargetingRule>>(response.TargetingRules, JsonDefaults.JsonOptions);
		deserializedRules.ShouldNotBeNull();
		deserializedRules.Count.ShouldBe(3);

		// Verify string rule
		var stringRule = deserializedRules.FirstOrDefault(r => r.Attribute == "userId");
		stringRule.ShouldNotBeNull();
		stringRule.ShouldBeOfType<StringTargetingRule>();
		stringRule.Operator.ShouldBe(TargetingOperator.Equals);
		stringRule.Variation.ShouldBe("premium");

		// Verify numeric rule
		var numericRule = deserializedRules.FirstOrDefault(r => r.Attribute == "age");
		numericRule.ShouldNotBeNull();
		numericRule.ShouldBeOfType<NumericTargetingRule>();
		numericRule.Operator.ShouldBe(TargetingOperator.GreaterThan);
		numericRule.Variation.ShouldBe("adult");
	}

	[Fact]
	public void Constructor_WithEmptyTargetingRules_SerializesToEmptyArray()
	{
		// Arrange
		var flag = CreateTestFeatureFlag();
		flag.TargetingRules = new List<ITargetingRule>();

		// Act
		var response = new FeatureFlagResponse(flag);

		// Assert
		response.TargetingRules.ShouldNotBeNull();
		response.TargetingRules.ShouldBe("[]");

		// Verify the JSON can be deserialized to an empty list
		var deserializedRules = JsonSerializer.Deserialize<List<ITargetingRule>>(response.TargetingRules, JsonDefaults.JsonOptions);
		deserializedRules.ShouldNotBeNull();
		deserializedRules.ShouldBeEmpty();
	}

	private static FeatureFlag CreateTestFeatureFlag()
	{
		return new FeatureFlag
		{
			Key = "test-flag",
			Name = "Test Flag",
			Description = "Test flag for unit tests",
			EvaluationModeSet = new FlagEvaluationModeSet([FlagEvaluationMode.Enabled]),
			AuditRecord = new FlagAuditRecord(DateTime.UtcNow, "test-user"),
			Schedule = FlagActivationSchedule.Unscheduled,
			OperationalWindow = FlagOperationalWindow.AlwaysOpen,
			Lifecycle = FlagLifecycle.DefaultLifecycle,
			UserAccess = FlagUserAccessControl.Unrestricted,
			TenantAccess = FlagTenantAccessControl.Unrestricted,
			Variations = FlagVariations.OnOff,
			Tags = new Dictionary<string, string>(),
			TargetingRules = new List<ITargetingRule>()
		};
	}
}
