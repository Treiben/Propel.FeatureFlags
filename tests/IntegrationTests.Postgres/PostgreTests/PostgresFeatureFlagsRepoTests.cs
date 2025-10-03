using FeatureFlags.IntegrationTests.Postgres.Support;
using Knara.UtcStrict;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace FeatureFlags.IntegrationTests.Postgres.PostgreTests.CoreRepository;

public class GetAsync_WithColumnMapping(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_ComplexFlagExists_ThenMapsAllEvaluationColumns()
	{
		// Arrange
		await fixture.ClearAllData();
		var options = new FlagOptionsBuilder()
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

		var identifier = new FlagIdentifier("complex-eval-flag", Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		var administration = new FlagAdministration(
			Name: "Complex Flag",
			Description: "Contains every evaluation option",
			RetentionPolicy: RetentionPolicy.OneMonthRetentionPolicy,
			Tags: new Dictionary<string, string> { { "eval-groups", "marketing" } },
			ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var featureFlag = new FeatureFlag(identifier, administration, options);
		await fixture.DashboardRepository.CreateAsync(featureFlag);

		// Act
		var result = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(identifier);

		// Assert - Verify all evaluation columns are correctly mapped
		result.ShouldNotBeNull();
		result.ModeSet.Contains([EvaluationMode.UserTargeted]).ShouldBeTrue();
		
		// Schedule mapping
		result.Schedule.EnableOn.DateTime.ShouldBeInRange(
			options.Schedule.EnableOn.DateTime.AddSeconds(-1), options.Schedule.EnableOn.DateTime.AddSeconds(1));
		result.Schedule.DisableOn.DateTime.ShouldBeInRange(
			options.Schedule.DisableOn.DateTime.AddSeconds(-1), options.Schedule.DisableOn.DateTime.AddSeconds(1));
		
		// Operational window mapping
		result.OperationalWindow.StartOn.ShouldBe(options.OperationalWindow.StartOn);
		result.OperationalWindow.StopOn.ShouldBe(options.OperationalWindow.StopOn);
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
		await fixture.ClearAllData();

		// Arrange
		var options = new FlagOptionsBuilder()
							.WithEvaluationModes(EvaluationMode.On)
							.Build();
		var identifier = new FlagIdentifier("minimal-flag", Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		var administration = new FlagAdministration(
			Name: "Minimal Flag",
			Description: "Contains only On mode",
			RetentionPolicy: RetentionPolicy.OneMonthRetentionPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var featureFlag = new FeatureFlag(identifier, administration, options);
		await fixture.DashboardRepository.CreateAsync(featureFlag);

		// Act
		var result = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(identifier);

		// Assert - Verify nullable columns are handled with defaults
		result.ShouldNotBeNull();
		result.OperationalWindow.TimeZone.ShouldBe("UTC");
		result.OperationalWindow.StopOn.ShouldBe(new TimeSpan(23, 59, 59));
		result.Variations.DefaultVariation.ShouldBe("");
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

		var options = new FlagOptionsBuilder()
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
		var identifier = new FlagIdentifier("json-test-flag", Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		var administration = new FlagAdministration(
			Name: "Jsonn Flag",
			Description: "Test deserialization",
			RetentionPolicy: RetentionPolicy.OneMonthRetentionPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var featureFlag = new FeatureFlag(identifier, administration, options);
		await fixture.DashboardRepository.CreateAsync(featureFlag);

		// Act
		var result = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(identifier);

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
		var result = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(flagKey);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagExistsButDifferentScope_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllData();

		var options = new FlagOptionsBuilder()
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
		var identifier = FlagIdentifier.CreateGlobal("json-test-flag");
		var administration = new FlagAdministration(
			Name: "Jsonn Flag",
			Description: "Test deserialization",
			RetentionPolicy: RetentionPolicy.OneMonthRetentionPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var featureFlag = new FeatureFlag(identifier, administration, options);
		await fixture.DashboardRepository.CreateAsync(featureFlag);

		// Act
		var identifier1 = new FlagIdentifier("json-test-flag", Scope.Application, "test", "12.0.0.0");
		var result = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(identifier1);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagExistsButDifferentApplication_ThenReturnsNull()
	{
		// Arrange
		var options = new FlagOptionsBuilder()
							.WithEvaluationModes(EvaluationMode.On)
							.Build();
		var identifier = new FlagIdentifier("a-flag", Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		var administration = new FlagAdministration(
			Name: "A Flag",
			Description: "Contains only On mode",
			RetentionPolicy: RetentionPolicy.OneMonthRetentionPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var featureFlag = new FeatureFlag(identifier, administration, options);
		await fixture.DashboardRepository.CreateAsync(featureFlag);

		var differentAppKey = new FlagIdentifier("app-flag",
			Scope.Application,
			"different-app",
			"1.0.0.0");

		// Act
		var result = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(differentAppKey);

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

		// Act
		var identifier = new FlagIdentifier("new-eval-flag", Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(identifier, EvaluationMode.On, 
			"new-eval-flag", "created by tester");

		// Assert - Verify flag was created by retrieving it
		var result = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(identifier);
		result.ShouldNotBeNull();
		result.ModeSet.Contains([EvaluationMode.On]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_DuplicateFlag_ThenSkipsCreation()
	{
		// Arrange
		await fixture.ClearAllData();

		var identifier = new FlagIdentifier("duplicate-eval-flag", Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(identifier, EvaluationMode.On,
			"duplicate-eval-flag", "created by tester");

		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(identifier, EvaluationMode.Off,
			"duplicate-eval-flag", "created by tester");

		// Assert - Verify original flag data is preserved (not overwritten)
		var result = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(identifier);

		result.ShouldNotBeNull();
		result.ModeSet.Contains([EvaluationMode.On]).ShouldBeTrue();
		result.ModeSet.Contains([EvaluationMode.Off]).ShouldBeFalse();
	}
}