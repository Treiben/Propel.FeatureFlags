using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace FeatureFlags.IntegrationTests.Postgres;

public class GetAsync_WithColumnMapping(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task If_ComplexFlagExists_ThenMapsAllEvaluationColumns()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = TestHelpers.SetupTestCases("complex-eval-flag", EvaluationMode.UserTargeted);
		
		// Set up complex data to test column mapping
		flag.Schedule = ActivationSchedule.CreateSchedule(DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(7));
		flag.OperationalWindow = new OperationalWindow(
			TimeSpan.FromHours(9), 
			TimeSpan.FromHours(17), 
			"America/New_York", 
			[DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]);
		flag.UserAccessControl = new AccessControl(
			allowed: ["user1", "user2"], 
			blocked: ["blocked1"], 
			rolloutPercentage: 75);
		flag.TenantAccessControl = new AccessControl(
			allowed: ["tenant1"], 
			rolloutPercentage: 50);
		flag.Variations = new Variations 
		{ 
			Values = new Dictionary<string, object> { { "control", "red" }, { "treatment", "blue" } },
			DefaultVariation = "control"
		};
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule("region", TargetingOperator.In, ["US", "CA"], "treatment")
		];

		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.EvaluationRepository.GetAsync(flag.Key);

		// Assert - Verify all evaluation columns are correctly mapped
		result.ShouldNotBeNull();
		result.FlagKey.ShouldBe(flag.Key.Key);
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserTargeted]).ShouldBeTrue();
		
		// Schedule mapping
		result.Schedule.EnableOn.Date.ShouldBe(flag.Schedule.EnableOn.Date);
		result.Schedule.DisableOn.Date.ShouldBe(flag.Schedule.DisableOn.Date);
		
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
		var (flag, _) = TestHelpers.SetupTestCases("minimal-flag", EvaluationMode.Enabled);
		// Keep minimal data to test null handling
		
		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.EvaluationRepository.GetAsync(flag.Key);

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
		var (flag, _) = TestHelpers.SetupTestCases("json-test-flag", EvaluationMode.UserTargeted);
		
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule("environment", TargetingOperator.Equals, ["production"], "prod-variation"),
			TargetingRuleFactory.CreateTargetingRule("version", TargetingOperator.GreaterThan, ["2.0"], "new-feature")
		];
		flag.Variations = new Variations 
		{ 
			Values = new Dictionary<string, object> 
			{ 
				{ "off", false }, 
				{ "on", true },
				{ "percentage", 0.75 },
				{ "config", new { timeout = 5000, retries = 3 } }
			}
		};

		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.EvaluationRepository.GetAsync(flag.Key);

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

public class GetAsync_WithNonExistentFlag(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagKey = new FlagKey("non-existent-flag", Scope.Application, "test-app", "1.0");

		// Act
		var result = await fixture.EvaluationRepository.GetAsync(flagKey);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagExistsButDifferentScope_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = TestHelpers.SetupTestCases("scoped-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var differentScopeKey = new FlagKey(flag.Key.Key, Scope.Global, flag.Key.ApplicationName, flag.Key.ApplicationVersion);

		// Act
		var result = await fixture.EvaluationRepository.GetAsync(differentScopeKey);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagExistsButDifferentApplication_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = TestHelpers.SetupTestCases("app-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var differentAppKey = new FlagKey(flag.Key.Key, flag.Key.Scope, "different-app", flag.Key.ApplicationVersion);

		// Act
		var result = await fixture.EvaluationRepository.GetAsync(differentAppKey);

		// Assert
		result.ShouldBeNull();
	}
}

public class CreateAsync_WithAuditTrail(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task If_NewFlag_ThenCreatesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = TestHelpers.SetupTestCases("new-eval-flag", EvaluationMode.Enabled);

		// Act
		await fixture.EvaluationRepository.CreateAsync(flag);

		// Assert - Verify flag was created by retrieving it
		var result = await fixture.EvaluationRepository.GetAsync(flag.Key);
		result.ShouldNotBeNull();
		result.FlagKey.ShouldBe(flag.Key.Key);
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_DuplicateFlag_ThenSkipsCreation()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = TestHelpers.SetupTestCases("duplicate-eval-flag", EvaluationMode.Enabled);
		await fixture.EvaluationRepository.CreateAsync(flag);

		// Modify flag data to ensure we can detect if duplicate creation occurs
		flag.Name = "Modified Name";
		flag.Description = "Modified Description";

		// Act - Try to create the same flag again
		await fixture.EvaluationRepository.CreateAsync(flag);

		// Assert - Verify original flag data is preserved (not overwritten)
		var result = await fixture.ManagementRepository.GetAsync(flag.Key);
		result.ShouldNotBeNull();
		result.Name.ShouldNotBe("Modified Name");
		result.Description.ShouldNotBe("Modified Description");
	}

	[Fact]
	public async Task If_CreateWithComplexData_ThenPreservesAllFields()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag, _) = TestHelpers.SetupTestCases("complex-create-flag", EvaluationMode.TenantRolloutPercentage);
		
		flag.UserAccessControl = new AccessControl(allowed: ["admin"], rolloutPercentage: 25);
		flag.TenantAccessControl = new AccessControl(blocked: ["test-tenant"], rolloutPercentage: 80);
		flag.Variations = new Variations 
		{ 
			Values = new Dictionary<string, object> { { "feature-on", true }, { "feature-off", false } },
			DefaultVariation = "feature-off"
		};

		// Act
		await fixture.ManagementRepository.CreateAsync(flag);

		// Assert - Verify all data is preserved through create and retrieval
		var result = await fixture.EvaluationRepository.GetAsync(flag.Key);
		result.ShouldNotBeNull();
		result.UserAccessControl.Allowed.ShouldContain("admin");
		result.UserAccessControl.RolloutPercentage.ShouldBe(25);
		result.TenantAccessControl.Blocked.ShouldContain("test-tenant");
		result.TenantAccessControl.RolloutPercentage.ShouldBe(80);
		result.Variations.DefaultVariation.ShouldBe("feature-off");
	}
}