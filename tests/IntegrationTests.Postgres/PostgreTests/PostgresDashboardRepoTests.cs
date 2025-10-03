using Knara.UtcStrict;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.Postgres.PostgreTests.Dashboard;

public class GetAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_FlagExistsWithMetadata_ThenReturnsCompleteFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("comprehensive-flag", Scope.Application, "test-app", "2.1.0");
		var metadata = new FlagAdministration(Name:"Comprehensive Test Flag",
			Description: "Complete field mapping test with all possible values",
			Tags: new Dictionary<string, string> { { "category", "testing" }, { "priority", "high" }, { "env", "staging" } },
			RetentionPolicy: new RetentionPolicy(DateTimeOffset.UtcNow.AddDays(45)),
			ChangeHistory: [new AuditTrail(DateTimeOffset.UtcNow.AddDays(-5), "test-creator", "flag-created", "Initial creation with full details")]
		);

		var scheduleBase = DateTimeOffset.UtcNow;
		var configuration = new FlagEvaluationOptions(
			ModeSet: new ModeSet([EvaluationMode.UserTargeted, EvaluationMode.Scheduled, EvaluationMode.TenantTargeted]),
			Schedule: UtcSchedule.CreateSchedule(scheduleBase.AddDays(2), scheduleBase.AddDays(10)),
			OperationalWindow: new UtcTimeWindow(
				TimeSpan.FromHours(8),
				TimeSpan.FromHours(18),
				"America/New_York",
				[DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]),
			TargetingRules: [
				TargetingRuleFactory.CreateTargetingRule("environment", TargetingOperator.Equals, ["production"], "prod-variant"),
				TargetingRuleFactory.CreateTargetingRule("region", TargetingOperator.In, ["US", "CA", "UK"], "regional-variant"),
				TargetingRuleFactory.CreateTargetingRule("version", TargetingOperator.GreaterThan, ["2.0"], "new-version-variant")
			],
			UserAccessControl: new AccessControl(
				allowed: ["user1", "user2", "user3", "admin-user"],
				blocked: ["blocked-user1", "blocked-user2", "suspended-user"],
				rolloutPercentage: 75),
			TenantAccessControl: new AccessControl(
				allowed: ["tenant-alpha", "tenant-beta", "tenant-gamma"],
				blocked: ["blocked-tenant", "suspended-tenant"],
				rolloutPercentage: 60),
			Variations: new Variations
			{
				Values = new Dictionary<string, object>
				{
					{ "control", false },
					{ "treatment", true },
					{ "percentage", 0.85 },
					{ "config", new { feature = "enabled", timeout = 5000, retries = 3 } },
					{ "colors", new[] { "red", "blue", "green" } }
				},
				DefaultVariation = "control"
			});
		var flag = new FeatureFlag(flagIdentifier, metadata, configuration);

		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.GetByKeyAsync(flagIdentifier);

		// Assert - Comprehensive field mapping verification
		result.ShouldNotBeNull();
		
		// Identifier fields
		result.Identifier.Key.ShouldBe("comprehensive-flag");
		result.Identifier.Scope.ShouldBe(Scope.Application);
		result.Identifier.ApplicationName.ShouldBe("test-app");
		result.Identifier.ApplicationVersion.ShouldBe("2.1.0");
		
		// Metadata fields
		result.Administration.Name.ShouldBe("Comprehensive Test Flag");
		result.Administration.Description.ShouldBe("Complete field mapping test with all possible values");
		result.Administration.Tags.Count.ShouldBe(3);

		result.Administration.Tags["category"].ShouldBe("testing");
		result.Administration.Tags["priority"].ShouldBe("high");
		result.Administration.Tags["env"].ShouldBe("staging");

		result.Administration.RetentionPolicy.IsPermanent.ShouldBeFalse();
		result.Administration.RetentionPolicy.ExpirationDate.DateTime
			.ShouldBeInRange(
				metadata.RetentionPolicy.ExpirationDate.DateTime.AddSeconds(-1),
				metadata.RetentionPolicy.ExpirationDate.DateTime.AddSeconds(1));

		var lastHistoryItem = result.Administration.ChangeHistory[^1];
		lastHistoryItem.Actor.ShouldBe("test-creator");
		lastHistoryItem.Action.ShouldBe("flag-created");
		lastHistoryItem.Notes.ShouldBe("Initial creation with full details");
		
		// Configuration - Evaluation modes
		result.EvaluationOptions.ModeSet.Contains([EvaluationMode.UserTargeted]).ShouldBeTrue();
		result.EvaluationOptions.ModeSet.Contains([EvaluationMode.Scheduled]).ShouldBeTrue();
		result.EvaluationOptions.ModeSet.Contains([EvaluationMode.TenantTargeted]).ShouldBeTrue();
		
		// Schedule mapping
		result.EvaluationOptions.Schedule.EnableOn.DateTime.ShouldBeInRange(
			configuration.Schedule.EnableOn.DateTime.AddSeconds(-1), configuration.Schedule.EnableOn.DateTime.AddSeconds(1));
		result.EvaluationOptions.Schedule.DisableOn.DateTime.ShouldBeInRange(
			configuration.Schedule.DisableOn.DateTime.AddSeconds(-1), configuration.Schedule.DisableOn.DateTime.AddSeconds(1));
		
		// Operational window mapping
		result.EvaluationOptions.OperationalWindow.StartOn.ShouldBe(TimeSpan.FromHours(8));
		result.EvaluationOptions.OperationalWindow.StopOn.ShouldBe(TimeSpan.FromHours(18));
		result.EvaluationOptions.OperationalWindow.TimeZone.ShouldBe("America/New_York");
		result.EvaluationOptions.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Monday);
		result.EvaluationOptions.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Wednesday);
		result.EvaluationOptions.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Friday);
		result.EvaluationOptions.OperationalWindow.DaysActive.ShouldNotContain(DayOfWeek.Saturday);
		result.EvaluationOptions.OperationalWindow.DaysActive.ShouldNotContain(DayOfWeek.Sunday);
		
		// Access control mapping
		result.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("user1");
		result.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("admin-user");
		result.EvaluationOptions.UserAccessControl.Blocked.ShouldContain("blocked-user1");
		result.EvaluationOptions.UserAccessControl.Blocked.ShouldContain("suspended-user");
		result.EvaluationOptions.UserAccessControl.RolloutPercentage.ShouldBe(75);
		result.EvaluationOptions.TenantAccessControl.Allowed.ShouldContain("tenant-alpha");
		result.EvaluationOptions.TenantAccessControl.Allowed.ShouldContain("tenant-gamma");
		result.EvaluationOptions.TenantAccessControl.Blocked.ShouldContain("blocked-tenant");
		result.EvaluationOptions.TenantAccessControl.RolloutPercentage.ShouldBe(60);
		
		// Variations mapping
		result.EvaluationOptions.Variations.Values.Count.ShouldBe(5);
		result.EvaluationOptions.Variations.Values.ShouldContainKey("control");
		result.EvaluationOptions.Variations.Values.ShouldContainKey("treatment");
		result.EvaluationOptions.Variations.Values.ShouldContainKey("percentage");
		result.EvaluationOptions.Variations.Values.ShouldContainKey("config");
		result.EvaluationOptions.Variations.Values.ShouldContainKey("colors");
		result.EvaluationOptions.Variations.DefaultVariation.ShouldBe("control");
		
		// Targeting rules mapping
		result.EvaluationOptions.TargetingRules.Count.ShouldBe(3);
		var envRule = result.EvaluationOptions.TargetingRules.FirstOrDefault(r => r.Attribute == "environment");
		envRule.ShouldNotBeNull();
		envRule.Variation.ShouldBe("prod-variant");
		var regionRule = result.EvaluationOptions.TargetingRules.FirstOrDefault(r => r.Attribute == "region");
		regionRule.ShouldNotBeNull();
		regionRule.Variation.ShouldBe("regional-variant");
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNull()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("non-existent-dashboard-flag", Scope.Global);

		// Act
		var result = await fixture.DashboardRepository.GetByKeyAsync(flagIdentifier);

		// Assert
		result.ShouldBeNull();
	}
}

public class GetAllAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_MultipleFlagsExist_ThenReturnsOrderedList()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag1 = CreateTestFlag("zebra-flag", "Zebra Flag");
		var flag2 = CreateTestFlag("alpha-flag", "Alpha Flag");

		await fixture.DashboardRepository.CreateAsync(flag1);
		await fixture.DashboardRepository.CreateAsync(flag2);

		// Act
		var results = await fixture.DashboardRepository.GetAllAsync();

		// Assert
		results.ShouldNotBeEmpty();
		results.Count.ShouldBe(2);
		results.First().Administration.Name.ShouldBe("Alpha Flag");
		results.Last().Administration.Name.ShouldBe("Zebra Flag");
	}

	[Fact]
	public async Task If_NoFlagsExist_ThenReturnsEmptyList()
	{
		// Arrange
		await fixture.ClearAllData();

		// Act
		var results = await fixture.DashboardRepository.GetAllAsync();

		// Assert
		results.ShouldBeEmpty();
	}

	private static FeatureFlag CreateTestFlag(string key, string name)
	{
		var identifier = new FlagIdentifier(key, Scope.Global);
		var metadata = new FlagAdministration(
			Name: name,
			Description: "Test description",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var configuration = new FlagEvaluationOptions(
			ModeSet: new ModeSet([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: new Variations());
		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class GetPagedAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_RequestsFirstPage_ThenReturnsCorrectPagination()
	{
		// Arrange
		await fixture.ClearAllData();
		for (int i = 1; i <= 5; i++)
		{
			var flag = CreateTestFlag($"flag-{i:D2}", $"Flag {i}");
			await fixture.DashboardRepository.CreateAsync(flag);
		}

		// Act
		var result = await fixture.DashboardRepository.GetPagedAsync(1, 3);

		// Assert
		result.Items.Count.ShouldBe(3);
		result.TotalCount.ShouldBe(5);
		result.Page.ShouldBe(1);
		result.PageSize.ShouldBe(3);
		result.HasNextPage.ShouldBeTrue();
		result.HasPreviousPage.ShouldBeFalse();
	}

	[Fact]
	public async Task If_RequestsPageWithFilter_ThenAppliesFiltering()
	{
		// Arrange
		await fixture.ClearAllData();
		var appFlag = CreateTestFlag("app-flag", "App Flag", Scope.Application, "test-app");
		var globalFlag = CreateTestFlag("global-flag", "Global Flag", Scope.Global);

		await fixture.DashboardRepository.CreateAsync(appFlag);
		await fixture.DashboardRepository.CreateAsync(globalFlag);

		var filter = new FeatureFlagFilter(Scope: Scope.Application);

		// Act
		var result = await fixture.DashboardRepository.GetPagedAsync(1, 10, filter);

		// Assert
		result.Items.Count.ShouldBe(1);
		result.Items.First().Identifier.Scope.ShouldBe(Scope.Application);
	}

	private static FeatureFlag CreateTestFlag(string key, string name, Scope scope = Scope.Global, string? appName = null)
	{
		var identifier = new FlagIdentifier(key, scope, appName);
		var metadata = new FlagAdministration(
					Name: name,
					Description: "Test description",
					RetentionPolicy: RetentionPolicy.GlobalPolicy,
					Tags: [],
					ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var configuration = new FlagEvaluationOptions(
			ModeSet: new ModeSet([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: new Variations());

		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class CreateAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_ValidFlag_ThenCreatesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var identifier = new FlagIdentifier("create-test-flag", Scope.Global);
		var metadata = new FlagAdministration(
			Name: "Create Test Flag",
			Description:"Test flag creation",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]);
		
		var configuration = new FlagEvaluationOptions(
			ModeSet: new ModeSet([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: new Variations());
		var flag = new FeatureFlag(identifier, metadata, configuration);

		// Act
		var result = await fixture.DashboardRepository.CreateAsync(flag);

		// Assert
		result.ShouldNotBeNull();
		var retrieved = await fixture.DashboardRepository.GetByKeyAsync(identifier);
		retrieved.ShouldNotBeNull();
		retrieved.Administration.Name.ShouldBe("Create Test Flag");

		var lastHistoryItem = result.Administration.ChangeHistory[^1];
		lastHistoryItem.Action.ShouldBe("flag-created");
	}

	[Fact]
	public async Task If_FlagWithComplexConfiguration_ThenCreatesWithAllFields()
	{
		// Arrange
		await fixture.ClearAllData();
		var identifier = new FlagIdentifier("complex-create-flag", Scope.Application, "test-app");
		var metadata = new FlagAdministration(
				Name: "Complex Create Flag",
				Description: "Testing complex flag creation",
				RetentionPolicy: RetentionPolicy.GlobalPolicy,
				Tags: new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
				ChangeHistory: [AuditTrail.FlagCreated("test-creator")]);

		var configuration = new FlagEvaluationOptions(
				ModeSet: new ModeSet([EvaluationMode.UserTargeted,
											EvaluationMode.Scheduled,
											EvaluationMode.UserRolloutPercentage,
											EvaluationMode.TenantRolloutPercentage,
											EvaluationMode.TenantTargeted,
											EvaluationMode.TimeWindow,
											EvaluationMode.TargetingRules]),
				UtcSchedule.CreateSchedule(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(7)),
				OperationalWindow: new UtcTimeWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(17), daysActive: [DayOfWeek.Monday, DayOfWeek.Wednesday]),
				UserAccessControl: new AccessControl(allowed: ["user1"], blocked: ["user2"], rolloutPercentage: 50),
				TenantAccessControl: new AccessControl(allowed: ["tenant1"], blocked: ["tenant2"], rolloutPercentage: 50),
				TargetingRules: [
					TargetingRuleFactory.CreateTargetingRule("role", TargetingOperator.Equals, ["admin", "superuser"], "admin-variant"),
					TargetingRuleFactory.CreateTargetingRule("department", TargetingOperator.In, ["sales", "marketing"], "sales-variant")
				],
				Variations: new Variations
				{
					Values = new Dictionary<string, object> { { "variant1", "red" }, { "variant2", "blue" } },
					DefaultVariation = "variant1"
				});
		var flag = new FeatureFlag(identifier, metadata, configuration);

		// Act
		var result = await fixture.DashboardRepository.CreateAsync(flag);

		// Assert
		result.ShouldNotBeNull();

		var retrieved = await fixture.DashboardRepository.GetByKeyAsync(identifier);

		retrieved.ShouldNotBeNull();
		//verify all metadata fields mapped correctly
		retrieved.Administration.Name.ShouldBe(metadata.Name);
		retrieved.Administration.Description.ShouldBe(metadata.Description);
		retrieved.Administration.Tags.Count.ShouldBe(2);
		retrieved.Administration.Tags["env"].ShouldBe("staging");
		retrieved.Administration.Tags["team"].ShouldBe("devops");
		retrieved.Administration.RetentionPolicy.IsPermanent.ShouldBeTrue();
		retrieved.Administration.ChangeHistory.Count.ShouldBe(1);
		retrieved.Administration.ChangeHistory[0].Action.ShouldBe("flag-created");

		//verify all configuration fields mapped correctly
		retrieved.EvaluationOptions.ModeSet.Contains(
			[EvaluationMode.UserTargeted,
			EvaluationMode.Scheduled,
			EvaluationMode.UserTargeted, 
			EvaluationMode.UserRolloutPercentage, 
			EvaluationMode.TenantRolloutPercentage, 
			EvaluationMode.TenantTargeted,
			EvaluationMode.TimeWindow,
			EvaluationMode.TargetingRules]).ShouldBeTrue();

		// verify schedule with slight tolerance for time differences
		retrieved.EvaluationOptions.Schedule.EnableOn.DateTime
			.ShouldBeInRange(configuration.Schedule.EnableOn.DateTime.AddSeconds(-1),
			configuration.Schedule.EnableOn.DateTime.AddSeconds(1));
		retrieved.EvaluationOptions.Schedule.DisableOn.DateTime
			.ShouldBeInRange(configuration.Schedule.DisableOn.DateTime.AddSeconds(-1),
			configuration.Schedule.DisableOn.DateTime.AddSeconds(1));

		// operational window
		retrieved.EvaluationOptions.OperationalWindow.StartOn.ShouldBe(configuration.OperationalWindow.StartOn);
		retrieved.EvaluationOptions.OperationalWindow.StopOn.ShouldBe(configuration.OperationalWindow.StopOn);
		retrieved.EvaluationOptions.OperationalWindow.TimeZone.ShouldBe("UTC");
		retrieved.EvaluationOptions.OperationalWindow.DaysActive.Length.ShouldBe(2);
		retrieved.EvaluationOptions.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Monday);
		retrieved.EvaluationOptions.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Wednesday);

		// targeting rules
		retrieved.EvaluationOptions.TargetingRules.Count.ShouldBe(2);
		retrieved.EvaluationOptions.TargetingRules[0].Attribute.ShouldBe("role");
		retrieved.EvaluationOptions.TargetingRules[0].Operator.ShouldBe(TargetingOperator.Equals);
		retrieved.EvaluationOptions.TargetingRules[0].Variation.ShouldBe("admin-variant");

		retrieved.EvaluationOptions.TargetingRules[1].Attribute.ShouldBe("department");
		retrieved.EvaluationOptions.TargetingRules[1].Operator.ShouldBe(TargetingOperator.In);
		retrieved.EvaluationOptions.TargetingRules[1].Variation.ShouldBe("sales-variant");

		// variations
		retrieved.EvaluationOptions.Variations.Values.Count.ShouldBe(2);
		retrieved.EvaluationOptions.Variations.Values["variant1"].ToString().ShouldBe("red");
		retrieved.EvaluationOptions.Variations.Values["variant2"].ToString().ShouldBe("blue");
		retrieved.EvaluationOptions.Variations.DefaultVariation.ShouldBe("variant1");

		// user access control
		retrieved.EvaluationOptions.UserAccessControl.Allowed.Count.ShouldBe(1);
		retrieved.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("user1");
		retrieved.EvaluationOptions.UserAccessControl.Blocked.Count.ShouldBe(1);
		retrieved.EvaluationOptions.UserAccessControl.Blocked.ShouldContain("user2");
		retrieved.EvaluationOptions.UserAccessControl.RolloutPercentage.ShouldBe(50);

		// tenant access control
		retrieved.EvaluationOptions.TenantAccessControl.Allowed.Count.ShouldBe(1);
		retrieved.EvaluationOptions.TenantAccessControl.Allowed.ShouldContain("tenant1");
		retrieved.EvaluationOptions.TenantAccessControl.Blocked.Count.ShouldBe(1);
		retrieved.EvaluationOptions.TenantAccessControl.Blocked.ShouldContain("tenant2");
		retrieved.EvaluationOptions.TenantAccessControl.RolloutPercentage.ShouldBe(50);
	}
}

public class UpdateAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_FlagExists_ThenUpdatesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("update-test-flag", Scope.Global);
		var originalFlag = CreateTestFlag(flagIdentifier, "Original Name");
		await fixture.DashboardRepository.CreateAsync(originalFlag);

		var updatedFlag = originalFlag with { Administration = new FlagAdministration(Name: "Updated Name", 
				Description: "Updated Description", 
				RetentionPolicy: RetentionPolicy.GlobalPolicy, 
				Tags: originalFlag.Administration.Tags, 
				ChangeHistory: [.. originalFlag.Administration.ChangeHistory, AuditTrail.FlagModified("updater", "Updated for test")]) };

		// Act
		var result = await fixture.DashboardRepository.UpdateAsync(updatedFlag);

		// Assert
		result.ShouldNotBeNull();
		var retrieved = await fixture.DashboardRepository.GetByKeyAsync(flagIdentifier);
		retrieved.ShouldNotBeNull();
		retrieved.Administration.Name.ShouldBe("Updated Name");
		retrieved.Administration.ChangeHistory.Count.ShouldBe(2);
		retrieved.Administration.ChangeHistory[0].Actor.ShouldBe("updater");
	}

	private static FeatureFlag CreateTestFlag(FlagIdentifier identifier, string name)
	{
		var metadata = new FlagAdministration(
			Name: name,
			Description: "Test description",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]);

		var configuration = new FlagEvaluationOptions(
				ModeSet: new ModeSet([EvaluationMode.On]),
				Schedule: UtcSchedule.Unscheduled,
				OperationalWindow: UtcTimeWindow.AlwaysOpen,
				TargetingRules: [],
				UserAccessControl: AccessControl.Unrestricted,
				TenantAccessControl: AccessControl.Unrestricted,
				Variations: new Variations());
		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class DeleteAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_FlagExists_ThenDeletesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("delete-test-flag", Scope.Global);
		var flag = CreateTestFlag(flagIdentifier, "Delete Test Flag");
		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.DeleteAsync(flagIdentifier, "deleter", "Test deletion");

		// Assert
		//result.ShouldBeTrue();
		var retrieved = await fixture.DashboardRepository.GetByKeyAsync(flagIdentifier);
		retrieved.ShouldBeNull();
	}

	private static FeatureFlag CreateTestFlag(FlagIdentifier identifier, string name)
	{
		var metadata = new FlagAdministration(
			Name: name,
			Description: "Test description",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]);

		var configuration = new FlagEvaluationOptions(
				ModeSet: new ModeSet([EvaluationMode.On]),
				Schedule: UtcSchedule.Unscheduled,
				OperationalWindow: UtcTimeWindow.AlwaysOpen,
				TargetingRules: [],
				UserAccessControl: AccessControl.Unrestricted,
				TenantAccessControl: AccessControl.Unrestricted,
				Variations: new Variations());
		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class FeatureFlagRepositoryComprehensiveTests(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_FlagWithMinMaxDateTimeValues_ThenMapsCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("minmax-datetime-flag", Scope.Global);

		var metadata = new FlagAdministration(
				Name: "Min/Max DateTime Test Flag",
				Description: "Testing min/max datetime boundary values",
				RetentionPolicy: RetentionPolicy.GlobalPolicy,
				Tags: new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
				ChangeHistory: [new AuditTrail(new UtcDateTime(DateTime.MinValue.AddYears(1970)), "system", "flag-created", "Created with min datetime")]);

		var configuration = new FlagEvaluationOptions(
				ModeSet: new ModeSet([EvaluationMode.Scheduled]),
				Schedule: UtcSchedule.CreateSchedule(new UtcDateTime(DateTime.MinValue.AddYears(3000)), new UtcDateTime(DateTime.MaxValue.AddYears(-1))),
				OperationalWindow: UtcTimeWindow.AlwaysOpen,
				TargetingRules: [],
				UserAccessControl: AccessControl.Unrestricted,
				TenantAccessControl: AccessControl.Unrestricted,
				Variations: new Variations());

		var flag = new FeatureFlag(flagIdentifier, metadata, configuration);

		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.GetByKeyAsync(flagIdentifier);

		// Assert - Verify min/max datetime handling
		result.ShouldNotBeNull();
		result.EvaluationOptions.Schedule.EnableOn.DateTime.Year.ShouldBe(3001);
		result.EvaluationOptions.Schedule.DisableOn.DateTime.Year.ShouldBeGreaterThan(9000);
		result.Administration.RetentionPolicy.IsPermanent.ShouldBeTrue();
		result.Administration.RetentionPolicy.ExpirationDate.DateTime.ShouldBe(DateTime.MaxValue.ToUniversalTime());
		result.Administration.ChangeHistory[^1].Timestamp.DateTime.Year.ShouldBe(1971);
	}

	[Fact]
	public async Task If_FlagWithAllNullableFields_ThenHandlesDefaultsCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("minimal-nullable-flag", Scope.Global);
		var metadata = new FlagAdministration(
								Name: "Minimal Nullable Flag",
								Description: "Testing null/default values for optional fields",
								RetentionPolicy: RetentionPolicy.GlobalPolicy,
								Tags: [],
								ChangeHistory: [AuditTrail.FlagCreated("test-user")]
							);

		var configuration = new FlagEvaluationOptions(
				ModeSet: new ModeSet([EvaluationMode.On]),
				Schedule: UtcSchedule.Unscheduled,
				OperationalWindow: UtcTimeWindow.AlwaysOpen,
				TargetingRules: [],
				UserAccessControl: AccessControl.Unrestricted,
				TenantAccessControl: AccessControl.Unrestricted,
				Variations: new Variations
				{
					Values = new Dictionary<string, object>()
					{
						{ "test-on", true },
						{ "test-off", false },
					},
					DefaultVariation = "test-off"
				});

		var flag = new FeatureFlag(flagIdentifier, metadata, configuration);

		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.GetByKeyAsync(flagIdentifier);

		// Assert - Verify default/null value handling
		result.ShouldNotBeNull();
		result.Administration.Tags.ShouldBeEmpty();

		result.Administration.ChangeHistory[^1].ShouldNotBeNull();
		result.Administration.ChangeHistory[^1].Actor.ShouldBe(metadata.ChangeHistory[^1].Actor);
		result.Administration.ChangeHistory[^1].Action.ShouldBe(metadata.ChangeHistory[^1].Action);

		result.EvaluationOptions.Schedule.ShouldBe(UtcSchedule.Unscheduled);

		result.EvaluationOptions.OperationalWindow.TimeZone.ShouldBe("UTC");
		result.EvaluationOptions.OperationalWindow.StartOn.ShouldBe(TimeSpan.Zero);
		result.EvaluationOptions.OperationalWindow.StopOn.ShouldBe(new TimeSpan(23, 59, 59));
		result.EvaluationOptions.OperationalWindow.DaysActive.Length.ShouldBe(7);

		result.EvaluationOptions.TargetingRules.ShouldBeEmpty();

		result.EvaluationOptions.UserAccessControl.Allowed.ShouldBeEmpty();
		result.EvaluationOptions.UserAccessControl.Blocked.ShouldBeEmpty();
		result.EvaluationOptions.UserAccessControl.RolloutPercentage.ShouldBe(100);

		result.EvaluationOptions.TenantAccessControl.Allowed.ShouldBeEmpty();
		result.EvaluationOptions.TenantAccessControl.Blocked.ShouldBeEmpty();
		result.EvaluationOptions.TenantAccessControl.RolloutPercentage.ShouldBe(100);

		result.EvaluationOptions.Variations.DefaultVariation.ShouldBe("test-off");
		result.EvaluationOptions.Variations.Values.Keys.ShouldContain("test-on");
		result.EvaluationOptions.Variations.Values.Keys.ShouldContain("test-off");
	}
}

public class UpdateMetadataAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_FlagExists_ThenUpdatesMetadataSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("metadata-update-flag", Scope.Global);
		var originalFlag = CreateTestFlag(flagIdentifier, "Original Name");
		await fixture.DashboardRepository.CreateAsync(originalFlag);

		var updatedMetadata = new FlagAdministration(
			Name: "Updated Metadata Name",
			Description: "Updated metadata description",
			RetentionPolicy: new RetentionPolicy(DateTimeOffset.UtcNow.AddDays(30)),
			Tags: new Dictionary<string, string> { { "updated", "true" }, { "version", "2.0" } },
			ChangeHistory: [.. originalFlag.Administration.ChangeHistory, AuditTrail.FlagModified("metadata-updater", "Metadata updated")]
		);

		var updatedFlag = originalFlag with { Administration = updatedMetadata };

		// Act
		var result = await fixture.DashboardRepository.UpdateMetadataAsync(updatedFlag);

		// Assert
		result.ShouldNotBeNull();
		var retrieved = await fixture.DashboardRepository.GetByKeyAsync(flagIdentifier);
		retrieved.ShouldNotBeNull();
		retrieved.Administration.Name.ShouldBe("Updated Metadata Name");
		retrieved.Administration.Description.ShouldBe("Updated metadata description");
		retrieved.Administration.Tags.Count.ShouldBe(2);
		retrieved.Administration.Tags["updated"].ShouldBe("true");
		retrieved.Administration.Tags["version"].ShouldBe("2.0");
		retrieved.Administration.RetentionPolicy.IsPermanent.ShouldBeFalse();
		retrieved.Administration.ChangeHistory.Count.ShouldBe(2);
		retrieved.Administration.ChangeHistory[0].Action.ShouldBe("flag-modified");
		retrieved.Administration.ChangeHistory[0].Actor.ShouldBe("metadata-updater");
	}

	[Fact]
	public async Task If_UpdateMetadata_ThenDoesNotAffectConfiguration()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("config-preserve-flag", Scope.Application, "test-app");
		var metadata = new FlagAdministration(
			Name: "Config Preserve Flag",
			Description: "Testing configuration preservation",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("creator")]
		);

		var originalConfig = new FlagEvaluationOptions(
			ModeSet: new ModeSet([EvaluationMode.UserTargeted, EvaluationMode.Scheduled]),
			Schedule: UtcSchedule.CreateSchedule(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(5)),
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [TargetingRuleFactory.CreateTargetingRule("env", TargetingOperator.Equals, ["prod"], "prod-variant")],
			UserAccessControl: new AccessControl(allowed: ["user1"], blocked: [], rolloutPercentage: 50),
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: new Variations { Values = new Dictionary<string, object> { { "on", true }, { "off", false } }, DefaultVariation = "off" }
		);

		var originalFlag = new FeatureFlag(flagIdentifier, metadata, originalConfig);
		await fixture.DashboardRepository.CreateAsync(originalFlag);

		var updatedMetadata = metadata with
		{
			Name = "Updated Name Only",
			Tags = new Dictionary<string, string> { { "changed", "yes" } },
			ChangeHistory = [.. metadata.ChangeHistory, AuditTrail.FlagModified("updater", "Name changed")]
		};

		var updatedFlag = originalFlag with { Administration = updatedMetadata };

		// Act
		await fixture.DashboardRepository.UpdateMetadataAsync(updatedFlag);

		// Assert
		var retrieved = await fixture.DashboardRepository.GetByKeyAsync(flagIdentifier);
		retrieved.ShouldNotBeNull();
		retrieved.Administration.Name.ShouldBe("Updated Name Only");
		retrieved.Administration.Tags["changed"].ShouldBe("yes");
		// Verify configuration unchanged
		retrieved.EvaluationOptions.ModeSet.Contains([EvaluationMode.UserTargeted]).ShouldBeTrue();
		retrieved.EvaluationOptions.ModeSet.Contains([EvaluationMode.Scheduled]).ShouldBeTrue();
		retrieved.EvaluationOptions.UserAccessControl.Allowed.ShouldContain("user1");
		retrieved.EvaluationOptions.UserAccessControl.RolloutPercentage.ShouldBe(50);
		retrieved.EvaluationOptions.TargetingRules.Count.ShouldBe(1);
		retrieved.EvaluationOptions.TargetingRules[0].Attribute.ShouldBe("env");
	}

	private static FeatureFlag CreateTestFlag(FlagIdentifier identifier, string name)
	{
		var metadata = new FlagAdministration(
			Name: name,
			Description: "Test description",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]
		);

		var configuration = new FlagEvaluationOptions(
			ModeSet: new ModeSet([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: new Variations()
		);

		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class FlagExistsAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_FlagExists_ThenReturnsTrue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("exists-test-flag", Scope.Global);
		var flag = CreateTestFlag(flagIdentifier, "Exists Test Flag");
		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.FlagExistsAsync(flagIdentifier);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsFalse()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("non-existent-flag", Scope.Application, "test-app");

		// Act
		var result = await fixture.DashboardRepository.FlagExistsAsync(flagIdentifier);

		// Assert
		result.ShouldBeFalse();
	}

	private static FeatureFlag CreateTestFlag(FlagIdentifier identifier, string name)
	{
		var metadata = new FlagAdministration(
			Name: name,
			Description: "Test description",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]
		);

		var configuration = new FlagEvaluationOptions(
			ModeSet: new ModeSet([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: new Variations()
		);

		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class FindAsync_WithDashboardRepository(PostgresTestsFixture fixture) : IClassFixture<PostgresTestsFixture>
{
	[Fact]
	public async Task If_FlagsNameContainsCriteriaName_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag1 = CreateTestFlag("search-flag-1", "Search Test Flag", "This is a searchable description");
		var flag2 = CreateTestFlag("search-flag-2", "Another Flag", "Different content");
		var flag3 = CreateTestFlag("different-key", "Search Test Flag", "Another description");

		await fixture.DashboardRepository.CreateAsync(flag1);
		await fixture.DashboardRepository.CreateAsync(flag2);
		await fixture.DashboardRepository.CreateAsync(flag3);

		var criteria = new FindFlagCriteria(Name: "sEaRcH TeSt");

		// Act
		var results = await fixture.DashboardRepository.FindAsync(criteria);

		// Assert
		results.ShouldNotBeEmpty();
		results.Count.ShouldBe(2);
		results.ShouldAllBe(f => f.Administration.Name.Contains("Search Test"));
	}

	[Fact]
	public async Task If_FlagsKeyMatchesCriteriaKeyExactly_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag1 = CreateTestFlag("search-flag-1", "Search Test Flag", "This is a searchable description");
		var flag2 = CreateTestFlag("search-flag-2", "Another Flag", "Different content");
		var flag3 = CreateTestFlag("different-key", "Search Test Flag", "Another description");

		await fixture.DashboardRepository.CreateAsync(flag1);
		await fixture.DashboardRepository.CreateAsync(flag2);
		await fixture.DashboardRepository.CreateAsync(flag3);

		var criteria = new FindFlagCriteria(Key: "sEaRcH-FlAg-2");

		// Act
		var results = await fixture.DashboardRepository.FindAsync(criteria);

		// Assert
		results.ShouldNotBeEmpty();
		results.Count.ShouldBe(1);
		results.ShouldAllBe(f => f.Administration.Name.Contains("Another Flag"));
		results.ShouldAllBe(f => f.Administration.Description.Contains("Different content"));
	}

	[Fact]
	public async Task If_FlagsDescriptionContainsCriteriaDescription_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag1 = CreateTestFlag("search-flag-1", "Search Test Flag", "This is a searchable description");
		var flag2 = CreateTestFlag("search-flag-2", "Another Flag", "Different content");
		var flag3 = CreateTestFlag("different-key", "Search Test Flag", "Another description");

		await fixture.DashboardRepository.CreateAsync(flag1);
		await fixture.DashboardRepository.CreateAsync(flag2);
		await fixture.DashboardRepository.CreateAsync(flag3);

		var criteria = new FindFlagCriteria(Description: "DeScRiPtIoN");

		// Act
		var results = await fixture.DashboardRepository.FindAsync(criteria);

		// Assert
		results.ShouldNotBeEmpty();
		results.Count.ShouldBe(2);
	}

	[Fact]
	public async Task If_NoFlagsMatchCriteria_ThenReturnsEmptyList()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = CreateTestFlag("test-flag", "Test Flag", "Test description");
		await fixture.DashboardRepository.CreateAsync(flag);

		var criteria = new FindFlagCriteria { Name = "NonExistentName" };

		// Act
		var results = await fixture.DashboardRepository.FindAsync(criteria);

		// Assert
		results.ShouldBeEmpty();
	}

	private static FeatureFlag CreateTestFlag(string key, string name, string description)
	{
		var identifier = new FlagIdentifier(key, Scope.Global);
		var metadata = new FlagAdministration(
			Name: name,
			Description: description,
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]
		);

		var configuration = new FlagEvaluationOptions(
			ModeSet: new ModeSet([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: new Variations()
		);

		return new FeatureFlag(identifier, metadata, configuration);
	}
}