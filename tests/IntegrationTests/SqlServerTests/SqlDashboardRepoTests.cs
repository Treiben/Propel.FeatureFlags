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
		var metadata = new Metadata
		{
			FlagIdentifier = flagIdentifier,
			Name = "Comprehensive Test Flag",
			Description = "Complete field mapping test with all possible values",
			Tags = new Dictionary<string, string> { { "category", "testing" }, { "priority", "high" }, { "env", "staging" } },
			RetentionPolicy = new RetentionPolicy(DateTimeOffset.UtcNow.AddDays(45)),
			Created = new AuditTrail(DateTimeOffset.UtcNow.AddDays(-5), "test-creator", "flag-created", "Initial creation with full details")
		};

		var scheduleBase = DateTimeOffset.UtcNow;
		var configuration = new FlagEvaluationConfiguration(
			identifier: flagIdentifier,
			activeEvaluationModes: new EvaluationModes([EvaluationMode.UserTargeted, EvaluationMode.Scheduled, EvaluationMode.TenantTargeted]),
			schedule: UtcSchedule.CreateSchedule(scheduleBase.AddDays(2), scheduleBase.AddDays(10)),
			operationalWindow: new UtcTimeWindow(
				TimeSpan.FromHours(8),
				TimeSpan.FromHours(18),
				"America/New_York",
				[DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]),
			targetingRules: [
				TargetingRuleFactory.CreateTargetingRule("environment", TargetingOperator.Equals, ["production"], "prod-variant"),
				TargetingRuleFactory.CreateTargetingRule("region", TargetingOperator.In, ["US", "CA", "UK"], "regional-variant"),
				TargetingRuleFactory.CreateTargetingRule("version", TargetingOperator.GreaterThan, ["2.0"], "new-version-variant")
			],
			userAccessControl: new AccessControl(
				allowed: ["user1", "user2", "user3", "admin-user"],
				blocked: ["blocked-user1", "blocked-user2", "suspended-user"],
				rolloutPercentage: 75),
			tenantAccessControl: new AccessControl(
				allowed: ["tenant-alpha", "tenant-beta", "tenant-gamma"],
				blocked: ["blocked-tenant", "suspended-tenant"],
				rolloutPercentage: 60),
			variations: new Variations
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
		result.Metadata.RetentionPolicy.ExpirationDate.Date.ShouldBe(DateTime.UtcNow.AddDays(45).Date);

		result.Metadata.Created.Actor.ShouldBe("test-creator");
		result.Metadata.Created.Action.ShouldBe("flag-created");
		result.Metadata.Created.Reason.ShouldBe("Initial creation with full details");

		result.Metadata.LastModified.ShouldNotBeNull();
		result.Metadata.LastModified.Actor.ShouldBe("test-creator");
		result.Metadata.LastModified.Action.ShouldBe("flag-created");
		result.Metadata.LastModified.Reason.ShouldBe("Initial creation with full details");
		
		// Configuration - Evaluation modes
		result.Configuration.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserTargeted]).ShouldBeTrue();
		result.Configuration.ActiveEvaluationModes.ContainsModes([EvaluationMode.Scheduled]).ShouldBeTrue();
		result.Configuration.ActiveEvaluationModes.ContainsModes([EvaluationMode.TenantTargeted]).ShouldBeTrue();
		
		// Schedule mapping
		result.Configuration.Schedule.EnableOn.DateTime.ShouldBeInRange(
			configuration.Schedule.EnableOn.DateTime.AddSeconds(-1), configuration.Schedule.EnableOn.DateTime.AddSeconds(1));
		result.Configuration.Schedule.DisableOn.DateTime.ShouldBeInRange(
			configuration.Schedule.DisableOn.DateTime.AddSeconds(-1), configuration.Schedule.DisableOn.DateTime.AddSeconds(1));
		
		// Operational window mapping
		result.Configuration.OperationalWindow.StartOn.ShouldBe(TimeSpan.FromHours(8));
		result.Configuration.OperationalWindow.StopOn.ShouldBe(TimeSpan.FromHours(18));
		result.Configuration.OperationalWindow.TimeZone.ShouldBe("America/New_York");
		result.Configuration.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Monday);
		result.Configuration.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Wednesday);
		result.Configuration.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Friday);
		result.Configuration.OperationalWindow.DaysActive.ShouldNotContain(DayOfWeek.Saturday);
		result.Configuration.OperationalWindow.DaysActive.ShouldNotContain(DayOfWeek.Sunday);
		
		// Access control mapping
		result.Configuration.UserAccessControl.Allowed.ShouldContain("user1");
		result.Configuration.UserAccessControl.Allowed.ShouldContain("admin-user");
		result.Configuration.UserAccessControl.Blocked.ShouldContain("blocked-user1");
		result.Configuration.UserAccessControl.Blocked.ShouldContain("suspended-user");
		result.Configuration.UserAccessControl.RolloutPercentage.ShouldBe(75);
		result.Configuration.TenantAccessControl.Allowed.ShouldContain("tenant-alpha");
		result.Configuration.TenantAccessControl.Allowed.ShouldContain("tenant-gamma");
		result.Configuration.TenantAccessControl.Blocked.ShouldContain("blocked-tenant");
		result.Configuration.TenantAccessControl.RolloutPercentage.ShouldBe(60);
		
		// Variations mapping
		result.Configuration.Variations.Values.Count.ShouldBe(5);
		result.Configuration.Variations.Values.ShouldContainKey("control");
		result.Configuration.Variations.Values.ShouldContainKey("treatment");
		result.Configuration.Variations.Values.ShouldContainKey("percentage");
		result.Configuration.Variations.Values.ShouldContainKey("config");
		result.Configuration.Variations.Values.ShouldContainKey("colors");
		result.Configuration.Variations.DefaultVariation.ShouldBe("control");
		
		// Targeting rules mapping
		result.Configuration.TargetingRules.Count.ShouldBe(3);
		var envRule = result.Configuration.TargetingRules.FirstOrDefault(r => r.Attribute == "environment");
		envRule.ShouldNotBeNull();
		envRule.Variation.ShouldBe("prod-variant");
		var regionRule = result.Configuration.TargetingRules.FirstOrDefault(r => r.Attribute == "region");
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
		var metadata = new Metadata
		{
			FlagIdentifier = identifier,
			Name = name,
			Description = "Test description",
			Created = AuditTrail.FlagCreated("test-user")
		};
		var configuration = new FlagEvaluationConfiguration(identifier, new EvaluationModes([EvaluationMode.On]));
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
		var metadata = new Metadata
		{
			FlagIdentifier = identifier,
			Name = name,
			Description = "Test description",
			Created = AuditTrail.FlagCreated("test-user")
		};
		var configuration = new FlagEvaluationConfiguration(identifier, new EvaluationModes([EvaluationMode.On]));
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
		var metadata = new Metadata
		{
			FlagIdentifier = identifier,
			Name = "Create Test Flag",
			Description = "Test flag creation",
			Created = AuditTrail.FlagCreated("creator")
		};
		var configuration = new FlagEvaluationConfiguration(identifier, new EvaluationModes([EvaluationMode.On]));
		var flag = new FeatureFlag(identifier, metadata, configuration);

		// Act
		var result = await fixture.DashboardRepository.CreateAsync(flag);

		// Assert
		result.ShouldNotBeNull();
		var retrieved = await fixture.DashboardRepository.GetAsync(identifier);
		retrieved.ShouldNotBeNull();
		retrieved.Metadata.Name.ShouldBe("Create Test Flag");
		retrieved.Metadata.Created.Action.ShouldBe("flag-created");
	}

	[Fact]
	public async Task If_FlagWithComplexConfiguration_ThenCreatesWithAllFields()
	{
		// Arrange
		await fixture.ClearAllData();
		var identifier = new FlagIdentifier("complex-create-flag", Scope.Application, "test-app");
		var metadata = new Metadata
		{
			FlagIdentifier = identifier,
			Name = "Complex Create Flag",
			Description = "Testing complex flag creation",
			Tags = new Dictionary<string, string> { { "env", "staging" }, { "team", "devops" } },
			Created = AuditTrail.FlagCreated("creator")
		};
		var configuration = new FlagEvaluationConfiguration(
				identifier,
				new EvaluationModes([EvaluationMode.UserTargeted, EvaluationMode.Scheduled]),
						UtcSchedule.CreateSchedule(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(7)),
						new UtcTimeWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(17)),
						userAccessControl: new AccessControl(["user1"], rolloutPercentage: 50),
						variations: new Variations
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
		retrieved.Configuration.ActiveEvaluationModes.ContainsModes([EvaluationMode.UserTargeted]).ShouldBeTrue();
		retrieved.Configuration.UserAccessControl.RolloutPercentage.ShouldBe(50);
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

		var updatedFlag = originalFlag;
		updatedFlag.Metadata.Name = "Updated Name";
		updatedFlag.Metadata.Description = "Updated description";
		updatedFlag.Metadata.LastModified = new AuditTrail(DateTimeOffset.UtcNow, "updater", "flag-updated", "Updated for test");

		// Act
		var result = await fixture.DashboardRepository.UpdateAsync(updatedFlag);

		// Assert
		result.ShouldNotBeNull();
		var retrieved = await fixture.DashboardRepository.GetAsync(flagIdentifier);
		retrieved.ShouldNotBeNull();
		retrieved.Metadata.Name.ShouldBe("Updated Name");
		retrieved.Metadata.LastModified.Action.ShouldBe("flag-modified");
	}

	private static FeatureFlag CreateTestFlag(FlagIdentifier identifier, string name)
	{
		var metadata = new Metadata
		{
			FlagIdentifier = identifier,
			Name = name,
			Description = "Test description",
			Created = AuditTrail.FlagCreated("test-user")
		};
		var configuration = new FlagEvaluationConfiguration(identifier, new EvaluationModes([EvaluationMode.On]));
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
		var metadata = new Metadata
		{
			FlagIdentifier = identifier,
			Name = name,
			Description = "Test description",
			Created = AuditTrail.FlagCreated("test-user")
		};
		var configuration = new FlagEvaluationConfiguration(identifier, new EvaluationModes([EvaluationMode.On]));
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
		var metadata = new Metadata
		{
			FlagIdentifier = flagIdentifier,
			Name = "Min/Max DateTime Test Flag",
			Description = "Testing min/max datetime boundary values",
			RetentionPolicy = new RetentionPolicy(UtcDateTime.MaxValue),
			Created = new AuditTrail(new UtcDateTime(DateTime.MinValue.AddYears(1970)), "system", "flag-created", "Created with min datetime")
		};
		var configuration = new FlagEvaluationConfiguration(
			identifier: flagIdentifier,
			activeEvaluationModes: new EvaluationModes([EvaluationMode.Scheduled]),
			schedule: UtcSchedule.CreateSchedule(new UtcDateTime(DateTime.MinValue.AddYears(3000)), new UtcDateTime(DateTime.MaxValue.AddYears(-1))));
		var flag = new FeatureFlag(flagIdentifier, metadata, configuration);

		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.GetAsync(flagIdentifier);

		// Assert - Verify min/max datetime handling
		result.ShouldNotBeNull();
		result.Configuration.Schedule.EnableOn.DateTime.Year.ShouldBe(3001);
		result.Configuration.Schedule.DisableOn.DateTime.Year.ShouldBeGreaterThan(9000);
		result.Metadata.RetentionPolicy.IsPermanent.ShouldBeTrue();
		result.Metadata.RetentionPolicy.ExpirationDate.ShouldBe(DateTime.MaxValue.ToUniversalTime());
		result.Metadata.Created.Timestamp.DateTime.Year.ShouldBe(1971);
	}

	[Fact]
	public async Task If_FlagWithAllNullableFields_ThenHandlesDefaultsCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flagIdentifier = new FlagIdentifier("minimal-nullable-flag", Scope.Global);
		var metadata = new Metadata
		{
			FlagIdentifier = flagIdentifier,
			Name = "Minimal Nullable Flag",
			Description = "Testing null/default values for optional fields",
			Tags = [],
			Created = AuditTrail.FlagCreated("test-user")
		};
		var configuration = new FlagEvaluationConfiguration(
			identifier: flagIdentifier,
			activeEvaluationModes: new EvaluationModes([EvaluationMode.On]));
		var flag = new FeatureFlag(flagIdentifier, metadata, configuration);

		await fixture.DashboardRepository.CreateAsync(flag);

		// Act
		var result = await fixture.DashboardRepository.GetAsync(flagIdentifier);

		// Assert - Verify default/null value handling
		result.ShouldNotBeNull();
		result.Metadata.Tags.ShouldBeEmpty();

		result.Metadata.LastModified.ShouldNotBeNull();
		result.Metadata.LastModified.Actor.ShouldBe(metadata.Created.Actor);
		result.Metadata.LastModified.Action.ShouldBe(metadata.Created.Action);

		result.Configuration.Schedule.ShouldBe(UtcSchedule.Unscheduled);

		result.Configuration.OperationalWindow.TimeZone.ShouldBe("UTC");
		result.Configuration.OperationalWindow.StartOn.ShouldBe(TimeSpan.Zero);
		result.Configuration.OperationalWindow.StopOn.ShouldBe(new TimeSpan(23, 59, 59));
		result.Configuration.OperationalWindow.DaysActive.Length.ShouldBe(7);

		result.Configuration.TargetingRules.ShouldBeEmpty();

		result.Configuration.UserAccessControl.Allowed.ShouldBeEmpty();
		result.Configuration.UserAccessControl.Blocked.ShouldBeEmpty();
		result.Configuration.UserAccessControl.RolloutPercentage.ShouldBe(100);

		result.Configuration.TenantAccessControl.Allowed.ShouldBeEmpty();
		result.Configuration.TenantAccessControl.Blocked.ShouldBeEmpty();
		result.Configuration.TenantAccessControl.RolloutPercentage.ShouldBe(100);

		result.Configuration.Variations.DefaultVariation.ShouldBe("off");
		result.Configuration.Variations.Values.ShouldContainKey("on");
		result.Configuration.Variations.Values.ShouldContainKey("off");
	}
}