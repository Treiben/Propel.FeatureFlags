using FeatureFlags.IntegrationTests.Support;
using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.PostgreTests;

public class GetAsync_WithColumnMapping(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_ComplexFlagExists_ThenMapsAllEvaluationColumns()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = new FlagConfigurationBuilder("complex-eval-flag")
				.WithEvaluationModes(EvaluationMode.UserTargeted)
				.WithSchedule(UtcSchedule.CreateSchedule(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(7)))
				.WithOperationalWindow(new UtcTimeWindow(
					TimeSpan.FromHours(9),
					TimeSpan.FromHours(17),
					"America/New_York",
					[DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]))
				.WithUserAccessControl(new AccessControl(
					allowed: ["user1", "user2"],
					blocked: ["blocked1"],
					rolloutPercentage: 75))
				.WithTenantAccessControl(new AccessControl(
					allowed: ["tenant1"],
					rolloutPercentage: 50))
				.WithVariations(new Variations
				{
					Values = new Dictionary<string, object> { { "control", "red" }, { "treatment", "blue" } },
					DefaultVariation = "control"
				})
				.WithTargetingRules([
					TargetingRuleFactory.CreateTargetingRule("region", TargetingOperator.In, ["US", "CA"], "treatment")
				])
				.Build();


		await fixture.SaveFlagAsync(flag, "complex-eval-flag", "created by integration tests");

		// Act
		var result = await fixture.FeatureFlagRepository.GetAsync(flag.Identifier);

		// Assert - Verify all evaluation columns are correctly mapped
		result.ShouldNotBeNull();
		result.Identifier.ShouldBe(flag.Identifier);
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserTargeted]).ShouldBeTrue();
		
		// Schedule mapping
		result.Schedule.EnableOn.DateTime.ShouldBeInRange(
			flag.Schedule.EnableOn.DateTime.AddSeconds(-1), flag.Schedule.EnableOn.DateTime.AddSeconds(1));
		result.Schedule.DisableOn.DateTime.ShouldBeInRange(
			flag.Schedule.DisableOn.DateTime.AddSeconds(-1), flag.Schedule.DisableOn.DateTime.AddSeconds(1));
		
		// Operational window mapping
		result.OperationalWindow.StartOn.ShouldBe(flag.OperationalWindow.StartOn);
		result.OperationalWindow.StopOn.ShouldBe(flag.OperationalWindow.StopOn);
		result.OperationalWindow.TimeZone.ShouldBe("America/New_York");
		result.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Monday);
		result.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Friday);
		
		// Access control mapping
		result.UserAccessControl.Allowed.ShouldContain("user1");
		result.UserAccessControl.Blocked.ShouldContain("blocked1");
		result.UserAccessControl.RolloutPercentage.ShouldBe(75);
		result.TenantAccessControl.RolloutPercentage.ShouldBe(50);
		
		// Variations mapping
		result.Variations.Values.ShouldContainKey("control");
		result.Variations.Values.ShouldContainKey("treatment");
		result.Variations.DefaultVariation.ShouldBe("control");
		
		// Targeting rules mapping
		result.TargetingRules.Count.ShouldBe(1);
		var rule = (StringTargetingRule)result.TargetingRules[0];
		rule.Attribute.ShouldBe("region");
		rule.Values.ShouldContain("US");
		rule.Variation.ShouldBe("treatment");
	}

	[Fact]
	public async Task If_FlagWithNullableColumns_ThenHandlesDefaultValues()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = new FlagConfigurationBuilder("minimal-flag")
							.WithEvaluationModes(EvaluationMode.On)
							.Build();

		await fixture.SaveFlagAsync(flag, "minimal-flag", "created by integration tests");

		// Act
		var result = await fixture.FeatureFlagRepository.GetAsync(flag.Identifier);

		// Assert - Verify nullable columns are handled with defaults
		result.ShouldNotBeNull();
		result.OperationalWindow.TimeZone.ShouldBe("UTC");
		result.OperationalWindow.StopOn.ShouldBe(new TimeSpan(23, 59, 59));
		result.Variations.DefaultVariation.ShouldBe("off");
		result.UserAccessControl.RolloutPercentage.ShouldBe(100);
		result.TenantAccessControl.RolloutPercentage.ShouldBe(100);
		result.TargetingRules.ShouldBeEmpty();
		result.UserAccessControl.Allowed.ShouldBeEmpty();
		result.TenantAccessControl.Allowed.ShouldBeEmpty();
	}

	[Fact]
	public async Task If_JsonColumns_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = new FlagConfigurationBuilder("json-test-flag")
			.WithEvaluationModes(EvaluationMode.UserTargeted)
			.WithTargetingRules([
				TargetingRuleFactory.CreateTargetingRule("environment", TargetingOperator.Equals, ["production"], "prod-variation"),
				TargetingRuleFactory.CreateTargetingRule("version", TargetingOperator.GreaterThan, ["2.0"], "new-feature")
			])
			.WithVariations(new Variations
			{
				Values = new Dictionary<string, object>
				{
					{ "off", false },
					{ "on", true },
					{ "percentage", 0.75 },
					{ "config", new { timeout = 5000, retries = 3 } }
				}
			})
			.Build();

		await fixture.SaveFlagAsync(flag, "json-test-flag", "created by integration tests");

		// Act
		var result = await fixture.FeatureFlagRepository.GetAsync(flag.Identifier);

		// Assert - Verify JSON deserialization
		result.ShouldNotBeNull();
		result.TargetingRules.Count.ShouldBe(2);
		
		var envRule = (StringTargetingRule)result.TargetingRules.First(r => r.Attribute == "environment");
		envRule.Values.ShouldContain("production");
		envRule.Variation.ShouldBe("prod-variation");
		
		result.Variations.Values.Count.ShouldBe(4);
		result.Variations.Values.ShouldContainKey("off");
		result.Variations.Values.ShouldContainKey("config");
	}
}

public class GetAsync_WithNonExistentFlag(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagKey = new FlagIdentifier("non-existent-flag", Scope.Application, "test-app", "1.0");

		// Act
		var result = await fixture.FeatureFlagRepository.GetAsync(flagKey);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagExistsButDifferentScope_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = new FlagConfigurationBuilder("scoped-flag")
								.WithEvaluationModes(EvaluationMode.On)
								.Build();

		await fixture.SaveFlagAsync(flag, "scoped-flag", "created by integration tests");

		var differentScopeKey = new FlagIdentifier(flag.Identifier.Key, Scope.Global);

		// Act
		var result = await fixture.FeatureFlagRepository.GetAsync(differentScopeKey);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagExistsButDifferentApplication_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = new FlagConfigurationBuilder("app-flag")
								.WithEvaluationModes(EvaluationMode.On)
								.Build();

		await fixture.SaveFlagAsync(flag, "app-flag", "created by integration tests");

		var differentAppKey = new FlagIdentifier(flag.Identifier.Key, 
			flag.Identifier.Scope, 
			"different-app",
			flag.Identifier.ApplicationVersion);

		// Act
		var result = await fixture.FeatureFlagRepository.GetAsync(differentAppKey);

		// Assert
		result.ShouldBeNull();
	}
}

public class CreateAsync_WithAuditTrail(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_NewFlag_ThenCreatesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = new FlagConfigurationBuilder("new-eval-flag")
								.WithEvaluationModes(EvaluationMode.On)
								.Build();

		// Act
		await fixture.FeatureFlagRepository.CreateAsync(flag.Identifier, EvaluationMode.On, 
			"new-eval-flag", "created by tester");

		// Assert - Verify flag was created by retrieving it
		var result = await fixture.FeatureFlagRepository.GetAsync(flag.Identifier);
		result.ShouldNotBeNull();
		result.Identifier.ShouldBe(flag.Identifier);
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.On]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_DuplicateFlag_ThenSkipsCreation()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = new FlagConfigurationBuilder("duplicate-eval-flag")
							.WithEvaluationModes(EvaluationMode.On)
							.Build();

		await fixture.FeatureFlagRepository.CreateAsync(flag.Identifier, EvaluationMode.On, "new-eval-flag", "created by tester");

		// Act - Try to create the same flag again
		await fixture.FeatureFlagRepository.CreateAsync(flag.Identifier, EvaluationMode.Off, "modified-eval-flag", "modified by tester");

		// Assert - Verify original flag data is preserved (not overwritten)
		var result = await fixture.FeatureFlagRepository.GetAsync(flag.Identifier);

		result.ShouldNotBeNull();
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.On]).ShouldBeTrue();
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.Off]).ShouldBeFalse();
	}
}