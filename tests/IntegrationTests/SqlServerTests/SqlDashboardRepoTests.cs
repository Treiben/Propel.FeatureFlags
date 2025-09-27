using Knara.UtcStrict;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace FeatureFlags.IntegrationTests.SqlServerTests;

public class GetAsync_WithDashboardRepository(SqlServerTestsFixture fixture) : IClassFixture<SqlServerTestsFixture>
{
	[Fact]
	public async Task If_FlagExistsWithMetadata_ThenReturnsCompleteFlag()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("comprehensive-flag", Scope.Application, "test-app", "2.1.0");
		var metadata = new Metadata(Name: "Comprehensive Test Flag",
			Description: "Complete field mapping test with all possible values",
			Tags: new Dictionary<string, string> { { "category", "testing" }, { "priority", "high" }, { "env", "staging" } },
			RetentionPolicy: new RetentionPolicy(DateTimeOffset.UtcNow.AddDays(45)),
			ChangeHistory: [new AuditTrail(DateTimeOffset.UtcNow.AddDays(-5), "test-creator", "flag-created", "Initial creation with full details")]
		);

		var scheduleBase = DateTimeOffset.UtcNow;
		var configuration = new EvalConfiguration(
			Modes: new EvaluationModes([EvaluationMode.UserTargeted, EvaluationMode.Scheduled, EvaluationMode.TenantTargeted]),
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
		var result = await fixture.DashboardRepository.GetAsync(flagIdentifier);

		// Assert - Comprehensive field mapping verification
		result.ShouldNotBeNull();

		// Identifier fields
		result.Identifier.Key.ShouldBe("comprehensive-flag");
		result.Identifier.Scope.ShouldBe(Scope.Application);
		result.Identifier.ApplicationName.ShouldBe("test-app");
		result.Identifier.ApplicationVersion.ShouldBe("2.1.0");

		// Metadata fields
		result.Metadata.Name.ShouldBe("Comprehensive Test Flag");
		result.Metadata.Description.ShouldBe("Complete field mapping test with all possible values");
		result.Metadata.Tags.Count.ShouldBe(3);

		result.Metadata.Tags["category"].ShouldBe("testing");
		result.Metadata.Tags["priority"].ShouldBe("high");
		result.Metadata.Tags["env"].ShouldBe("staging");

		result.Metadata.RetentionPolicy.IsPermanent.ShouldBeFalse();
		result.Metadata.RetentionPolicy.ExpirationDate.DateTime
			.ShouldBeInRange(
				metadata.RetentionPolicy.ExpirationDate.DateTime.AddSeconds(-1),
				metadata.RetentionPolicy.ExpirationDate.DateTime.AddSeconds(1));

		var lastHistoryItem = result.Metadata.ChangeHistory[^1];
		lastHistoryItem.Actor.ShouldBe("test-creator");
		lastHistoryItem.Action.ShouldBe("flag-created");
		lastHistoryItem.Notes.ShouldBe("Initial creation with full details");

		// Configuration - Evaluation modes
		result.EvalConfig.Modes.ContainsModes([EvaluationMode.UserTargeted]).ShouldBeTrue();
		result.EvalConfig.Modes.ContainsModes([EvaluationMode.Scheduled]).ShouldBeTrue();
		result.EvalConfig.Modes.ContainsModes([EvaluationMode.TenantTargeted]).ShouldBeTrue();

		// Schedule mapping
		result.EvalConfig.Schedule.EnableOn.DateTime.ShouldBeInRange(
			configuration.Schedule.EnableOn.DateTime.AddSeconds(-1), configuration.Schedule.EnableOn.DateTime.AddSeconds(1));
		result.EvalConfig.Schedule.DisableOn.DateTime.ShouldBeInRange(
			configuration.Schedule.DisableOn.DateTime.AddSeconds(-1), configuration.Schedule.DisableOn.DateTime.AddSeconds(1));

		// Operational window mapping
		result.EvalConfig.OperationalWindow.StartOn.ShouldBe(TimeSpan.FromHours(8));
		result.EvalConfig.OperationalWindow.StopOn.ShouldBe(TimeSpan.FromHours(18));
		result.EvalConfig.OperationalWindow.TimeZone.ShouldBe("America/New_York");
		result.EvalConfig.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Monday);
		result.EvalConfig.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Wednesday);
		result.EvalConfig.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Friday);
		result.EvalConfig.OperationalWindow.DaysActive.ShouldNotContain(DayOfWeek.Saturday);
		result.EvalConfig.OperationalWindow.DaysActive.ShouldNotContain(DayOfWeek.Sunday);

		// Access control mapping
		result.EvalConfig.UserAccessControl.Allowed.ShouldContain("user1");
		result.EvalConfig.UserAccessControl.Allowed.ShouldContain("admin-user");
		result.EvalConfig.UserAccessControl.Blocked.ShouldContain("blocked-user1");
		result.EvalConfig.UserAccessControl.Blocked.ShouldContain("suspended-user");
		result.EvalConfig.UserAccessControl.RolloutPercentage.ShouldBe(75);
		result.EvalConfig.TenantAccessControl.Allowed.ShouldContain("tenant-alpha");
		result.EvalConfig.TenantAccessControl.Allowed.ShouldContain("tenant-gamma");
		result.EvalConfig.TenantAccessControl.Blocked.ShouldContain("blocked-tenant");
		result.EvalConfig.TenantAccessControl.RolloutPercentage.ShouldBe(60);

		// Variations mapping
		result.EvalConfig.Variations.Values.Count.ShouldBe(5);
		result.EvalConfig.Variations.Values.ShouldContainKey("control");
		result.EvalConfig.Variations.Values.ShouldContainKey("treatment");
		result.EvalConfig.Variations.Values.ShouldContainKey("percentage");
		result.EvalConfig.Variations.Values.ShouldContainKey("config");
		result.EvalConfig.Variations.Values.ShouldContainKey("colors");
		result.EvalConfig.Variations.DefaultVariation.ShouldBe("control");

		// Targeting rules mapping
		result.EvalConfig.TargetingRules.Count.ShouldBe(3);
		var envRule = result.EvalConfig.TargetingRules.FirstOrDefault(r => r.Attribute == "environment");
		envRule.ShouldNotBeNull();
		envRule.Variation.ShouldBe("prod-variant");
		var regionRule = result.EvalConfig.TargetingRules.FirstOrDefault(r => r.Attribute == "region");
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
		var result = await fixture.DashboardRepository.GetAsync(flagIdentifier);

		// Assert
		result.ShouldBeNull();
	}
}

public class GetAllAsync_WithDashboardRepository(SqlServerTestsFixture fixture) : IClassFixture<SqlServerTestsFixture>
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
		results.First().Metadata.Name.ShouldBe("Alpha Flag");
		results.Last().Metadata.Name.ShouldBe("Zebra Flag");
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
		var metadata = new Metadata(
			Name: name,
			Description: "Test description",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var configuration = new EvalConfiguration(
			Modes: new EvaluationModes([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: Variations.OnOff);
		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class GetPagedAsync_WithDashboardRepository(SqlServerTestsFixture fixture) : IClassFixture<SqlServerTestsFixture>
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

		var filter = new FeatureFlagFilter { Scope = Scope.Application };

		// Act
		var result = await fixture.DashboardRepository.GetPagedAsync(1, 10, filter);

		// Assert
		result.Items.Count.ShouldBe(1);
		result.Items.First().Identifier.Scope.ShouldBe(Scope.Application);
	}

	private static FeatureFlag CreateTestFlag(string key, string name, Scope scope = Scope.Global, string? appName = null)
	{
		var identifier = new FlagIdentifier(key, scope, appName);
		var metadata = new Metadata(
					Name: name,
					Description: "Test description",
					RetentionPolicy: RetentionPolicy.GlobalPolicy,
					Tags: [],
					ChangeHistory: [AuditTrail.FlagCreated("test-user")]);

		var configuration = new EvalConfiguration(
			Modes: new EvaluationModes([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: Variations.OnOff);

		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class CreateAsync_WithDashboardRepository(SqlServerTestsFixture fixture) : IClassFixture<SqlServerTestsFixture>
{
	[Fact]
	public async Task If_ValidFlag_ThenCreatesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var identifier = new FlagIdentifier("create-test-flag", Scope.Global);
		var metadata = new Metadata(
			Name: "Create Test Flag",
			Description: "Test flag creation",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: [],
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]);

		var configuration = new EvalConfiguration(
			Modes: new EvaluationModes([EvaluationMode.On]),
			Schedule: UtcSchedule.Unscheduled,
			OperationalWindow: UtcTimeWindow.AlwaysOpen,
			TargetingRules: [],
			UserAccessControl: AccessControl.Unrestricted,
			TenantAccessControl: AccessControl.Unrestricted,
			Variations: Variations.OnOff);
		var flag = new FeatureFlag(identifier, metadata, configuration);

		// Act
		var result = await fixture.DashboardRepository.CreateAsync(flag);

		// Assert
		result.ShouldNotBeNull();
		var retrieved = await fixture.DashboardRepository.GetAsync(identifier);
		retrieved.ShouldNotBeNull();
		retrieved.Metadata.Name.ShouldBe("Create Test Flag");

		var lastHistoryItem = result.Metadata.ChangeHistory[^1];
		lastHistoryItem.Action.ShouldBe("flag-created");
	}

	[Fact]
	public async Task If_FlagWithComplexConfiguration_ThenCreatesWithAllFields()
	{
		// Arrange
		await fixture.ClearAllData();
		var identifier = new FlagIdentifier("complex-create-flag", Scope.Application, "test-app");
		var metadata = new Metadata(
				Name: "Complex Create Flag",
				Description: "Testing complex flag creation",
				RetentionPolicy: RetentionPolicy.GlobalPolicy,
				Tags: new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
				ChangeHistory: [AuditTrail.FlagCreated("test-creator")]);

		var configuration = new EvalConfiguration(
				Modes: new EvaluationModes([EvaluationMode.UserTargeted,
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

		var retrieved = await fixture.DashboardRepository.GetAsync(identifier);

		retrieved.ShouldNotBeNull();
		//verify all metadata fields mapped correctly
		retrieved.Metadata.Name.ShouldBe(metadata.Name);
		retrieved.Metadata.Description.ShouldBe(metadata.Description);
		retrieved.Metadata.Tags.Count.ShouldBe(2);
		retrieved.Metadata.Tags["env"].ShouldBe("staging");
		retrieved.Metadata.Tags["team"].ShouldBe("devops");
		retrieved.Metadata.RetentionPolicy.IsPermanent.ShouldBeTrue();
		retrieved.Metadata.ChangeHistory.Count.ShouldBe(1);
		retrieved.Metadata.ChangeHistory[0].Action.ShouldBe("flag-created");

		//verify all configuration fields mapped correctly
		retrieved.EvalConfig.Modes.ContainsModes(
			[EvaluationMode.UserTargeted,
			EvaluationMode.Scheduled,
			EvaluationMode.UserTargeted,
			EvaluationMode.UserRolloutPercentage,
			EvaluationMode.TenantRolloutPercentage,
			EvaluationMode.TenantTargeted,
			EvaluationMode.TimeWindow,
			EvaluationMode.TargetingRules]).ShouldBeTrue();

		// verify schedule with slight tolerance for time differences
		retrieved.EvalConfig.Schedule.EnableOn.DateTime
			.ShouldBeInRange(configuration.Schedule.EnableOn.DateTime.AddSeconds(-1),
			configuration.Schedule.EnableOn.DateTime.AddSeconds(1));
		retrieved.EvalConfig.Schedule.DisableOn.DateTime
			.ShouldBeInRange(configuration.Schedule.DisableOn.DateTime.AddSeconds(-1),
			configuration.Schedule.DisableOn.DateTime.AddSeconds(1));

		// operational window
		retrieved.EvalConfig.OperationalWindow.StartOn.ShouldBe(configuration.OperationalWindow.StartOn);
		retrieved.EvalConfig.OperationalWindow.StopOn.ShouldBe(configuration.OperationalWindow.StopOn);
		retrieved.EvalConfig.OperationalWindow.TimeZone.ShouldBe("UTC");
		retrieved.EvalConfig.OperationalWindow.DaysActive.Length.ShouldBe(2);
		retrieved.EvalConfig.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Monday);
		retrieved.EvalConfig.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Wednesday);

		// targeting rules
		retrieved.EvalConfig.TargetingRules.Count.ShouldBe(2);
		retrieved.EvalConfig.TargetingRules[0].Attribute.ShouldBe("role");
		retrieved.EvalConfig.TargetingRules[0].Operator.ShouldBe(TargetingOperator.Equals);
		retrieved.EvalConfig.TargetingRules[0].Variation.ShouldBe("admin-variant");

		retrieved.EvalConfig.TargetingRules[1].Attribute.ShouldBe("department");
		retrieved.EvalConfig.TargetingRules[1].Operator.ShouldBe(TargetingOperator.In);
		retrieved.EvalConfig.TargetingRules[1].Variation.ShouldBe("sales-variant");

		// variations
		retrieved.EvalConfig.Variations.Values.Count.ShouldBe(2);
		retrieved.EvalConfig.Variations.Values["variant1"].ToString().ShouldBe("red");
		retrieved.EvalConfig.Variations.Values["variant2"].ToString().ShouldBe("blue");
		retrieved.EvalConfig.Variations.DefaultVariation.ShouldBe("variant1");

		// user access control
		retrieved.EvalConfig.UserAccessControl.Allowed.Count.ShouldBe(1);
		retrieved.EvalConfig.UserAccessControl.Allowed.ShouldContain("user1");
		retrieved.EvalConfig.UserAccessControl.Blocked.Count.ShouldBe(1);
		retrieved.EvalConfig.UserAccessControl.Blocked.ShouldContain("user2");
		retrieved.EvalConfig.UserAccessControl.RolloutPercentage.ShouldBe(50);

		// tenant access control
		retrieved.EvalConfig.TenantAccessControl.Allowed.Count.ShouldBe(1);
		retrieved.EvalConfig.TenantAccessControl.Allowed.ShouldContain("tenant1");
		retrieved.EvalConfig.TenantAccessControl.Blocked.Count.ShouldBe(1);
		retrieved.EvalConfig.TenantAccessControl.Blocked.ShouldContain("tenant2");
		retrieved.EvalConfig.TenantAccessControl.RolloutPercentage.ShouldBe(50);
	}
}

public class UpdateAsync_WithDashboardRepository(SqlServerTestsFixture fixture) : IClassFixture<SqlServerTestsFixture>
{
	[Fact]
	public async Task If_FlagExists_ThenUpdatesSuccessfully()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("update-test-flag", Scope.Global);
		var originalFlag = CreateTestFlag(flagIdentifier, "Original Name");
		await fixture.DashboardRepository.CreateAsync(originalFlag);

		var updatedFlag = originalFlag with
		{
			Metadata = new Metadata(Name: "Updated Name",
				Description: "Updated Description",
				RetentionPolicy: RetentionPolicy.GlobalPolicy,
				Tags: originalFlag.Metadata.Tags,
				ChangeHistory: [.. originalFlag.Metadata.ChangeHistory, AuditTrail.FlagModified("updater", "Updated for test")])
		};

		// Act
		var result = await fixture.DashboardRepository.UpdateAsync(updatedFlag);

		// Assert
		result.ShouldNotBeNull();
		var retrieved = await fixture.DashboardRepository.GetAsync(flagIdentifier);
		retrieved.ShouldNotBeNull();
		retrieved.Metadata.Name.ShouldBe("Updated Name");
		retrieved.Metadata.ChangeHistory.Count.ShouldBe(2);
		retrieved.Metadata.ChangeHistory[0].Actor.ShouldBe("updater");
	}

	private static FeatureFlag CreateTestFlag(FlagIdentifier identifier, string name)
	{
		var metadata = new Metadata(
			Name: name,
			Description: "Test description",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]);

		var configuration = new EvalConfiguration(
				Modes: new EvaluationModes([EvaluationMode.On]),
				Schedule: UtcSchedule.Unscheduled,
				OperationalWindow: UtcTimeWindow.AlwaysOpen,
				TargetingRules: [],
				UserAccessControl: AccessControl.Unrestricted,
				TenantAccessControl: AccessControl.Unrestricted,
				Variations: Variations.OnOff);
		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class DeleteAsync_WithDashboardRepository(SqlServerTestsFixture fixture) : IClassFixture<SqlServerTestsFixture>
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
		var retrieved = await fixture.DashboardRepository.GetAsync(flagIdentifier);
		retrieved.ShouldBeNull();
	}

	private static FeatureFlag CreateTestFlag(FlagIdentifier identifier, string name)
	{
		var metadata = new Metadata(
			Name: name,
			Description: "Test description",
			RetentionPolicy: RetentionPolicy.GlobalPolicy,
			Tags: new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
			ChangeHistory: [AuditTrail.FlagCreated("test-creator")]);

		var configuration = new EvalConfiguration(
				Modes: new EvaluationModes([EvaluationMode.On]),
				Schedule: UtcSchedule.Unscheduled,
				OperationalWindow: UtcTimeWindow.AlwaysOpen,
				TargetingRules: [],
				UserAccessControl: AccessControl.Unrestricted,
				TenantAccessControl: AccessControl.Unrestricted,
				Variations: Variations.OnOff);
		return new FeatureFlag(identifier, metadata, configuration);
	}
}

public class FeatureFlagRepositoryComprehensiveTests(SqlServerTestsFixture fixture) : IClassFixture<SqlServerTestsFixture>
{
	[Fact]
	public async Task If_FlagWithMinMaxDateTimeValues_ThenMapsCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("minmax-datetime-flag", Scope.Global);

		var metadata = new Metadata(
				Name: "Min/Max DateTime Test Flag",
				Description: "Testing min/max datetime boundary values",
				RetentionPolicy: RetentionPolicy.GlobalPolicy,
				Tags: new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
				ChangeHistory: [new AuditTrail(new UtcDateTime(DateTime.MinValue.AddYears(1970)), "system", "flag-created", "Created with min datetime")]);

		var configuration = new EvalConfiguration(
				Modes: new EvaluationModes([EvaluationMode.Scheduled]),
				Schedule: UtcSchedule.CreateSchedule(new UtcDateTime(DateTime.MinValue.AddYears(3000)), new UtcDateTime(DateTime.MaxValue.AddYears(-1))),
				OperationalWindow: UtcTimeWindow.AlwaysOpen,
				TargetingRules: [],
				UserAccessControl: AccessControl.Unrestricted,
				TenantAccessControl: AccessControl.Unrestricted,
				Variations: Variations.OnOff);

		var flag = new FeatureFlag(flagIdentifier, metadata, configuration);

		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.GetAsync(flagIdentifier);

		// Assert - Verify min/max datetime handling
		result.ShouldNotBeNull();
		result.EvalConfig.Schedule.EnableOn.DateTime.Year.ShouldBe(3001);
		result.EvalConfig.Schedule.DisableOn.DateTime.Year.ShouldBeGreaterThan(9000);
		result.Metadata.RetentionPolicy.IsPermanent.ShouldBeTrue();
		result.Metadata.RetentionPolicy.ExpirationDate.DateTime.ShouldBe(DateTime.MaxValue.ToUniversalTime());
		result.Metadata.ChangeHistory[^1].Timestamp.DateTime.Year.ShouldBe(1971);
	}

	[Fact]
	public async Task If_FlagWithAllNullableFields_ThenHandlesDefaultsCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("minimal-nullable-flag", Scope.Global);
		var metadata = new Metadata(
								Name: "Minimal Nullable Flag",
								Description: "Testing null/default values for optional fields",
								RetentionPolicy: RetentionPolicy.GlobalPolicy,
								Tags: [],
								ChangeHistory: [AuditTrail.FlagCreated("test-user")]
							);

		var configuration = new EvalConfiguration(
				Modes: new EvaluationModes([EvaluationMode.On]),
				Schedule: UtcSchedule.Unscheduled,
				OperationalWindow: UtcTimeWindow.AlwaysOpen,
				TargetingRules: [],
				UserAccessControl: AccessControl.Unrestricted,
				TenantAccessControl: AccessControl.Unrestricted,
				Variations: Variations.OnOff);

		var flag = new FeatureFlag(flagIdentifier, metadata, configuration);

		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.GetAsync(flagIdentifier);

		// Assert - Verify default/null value handling
		result.ShouldNotBeNull();
		result.Metadata.Tags.ShouldBeEmpty();

		result.Metadata.ChangeHistory[^1].ShouldNotBeNull();
		result.Metadata.ChangeHistory[^1].Actor.ShouldBe(metadata.ChangeHistory[^1].Actor);
		result.Metadata.ChangeHistory[^1].Action.ShouldBe(metadata.ChangeHistory[^1].Action);

		result.EvalConfig.Schedule.ShouldBe(UtcSchedule.Unscheduled);

		result.EvalConfig.OperationalWindow.TimeZone.ShouldBe("UTC");
		result.EvalConfig.OperationalWindow.StartOn.ShouldBe(TimeSpan.Zero);
		result.EvalConfig.OperationalWindow.StopOn.ShouldBe(new TimeSpan(23, 59, 59));
		result.EvalConfig.OperationalWindow.DaysActive.Length.ShouldBe(7);

		result.EvalConfig.TargetingRules.ShouldBeEmpty();

		result.EvalConfig.UserAccessControl.Allowed.ShouldBeEmpty();
		result.EvalConfig.UserAccessControl.Blocked.ShouldBeEmpty();
		result.EvalConfig.UserAccessControl.RolloutPercentage.ShouldBe(100);

		result.EvalConfig.TenantAccessControl.Allowed.ShouldBeEmpty();
		result.EvalConfig.TenantAccessControl.Blocked.ShouldBeEmpty();
		result.EvalConfig.TenantAccessControl.RolloutPercentage.ShouldBe(100);

		result.EvalConfig.Variations.DefaultVariation.ShouldBe("off");
		result.EvalConfig.Variations.Values.ShouldContainKey("on");
		result.EvalConfig.Variations.Values.ShouldContainKey("off");
	}
}