using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.IntegrationTests.Postgres;

public class GetAsync_WhenFlagExists(PostgresRepoTestsFixture fixture) : IClassFixture<PostgresRepoTestsFixture>
{
	[Fact]
	public async Task ThenReturnsFlag()
	{
		// Arrange
		var flag = TestHelpers.CreateTestFlag("get-test", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Repository.GetAsync("get-test");

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("get-test");
		result.Name.ShouldBe(flag.Name);
		result.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenDeserializesCorrectly()
	{
		// Arrange
		var flag = TestHelpers.CreateTestFlag("complex-flag", FlagEvaluationMode.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = ["US", "CA"],
				Variation = "region-specific"
			}
		];
		flag.UserAccess = new FlagUserAccessControl(allowedUsers: ["user1", "user2"]);
		flag.Tags = new Dictionary<string, string> { { "team", "platform" }, { "env", "test" } };

		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Repository.GetAsync("complex-flag");

		// Assert
		result.ShouldNotBeNull();
		result.TargetingRules.Count.ShouldBe(1);
		result.TargetingRules[0].Attribute.ShouldBe("region");
		result.TargetingRules[0].Values.ShouldContain("US");
		result.Tags.ShouldContainKeyAndValue("team", "platform");
	}
}

public class GetAsync_WhenFlagDoesNotExist(PostgresRepoTestsFixture fixture) : IClassFixture<PostgresRepoTestsFixture>
{
	[Fact]
	public async Task ThenReturnsNull()
	{
		// Act
		var result = await fixture.Repository.GetAsync("non-existent-flag");

		// Assert
		result.ShouldBeNull();
	}
}

public class GetAllAsync_WithMultipleFlags(PostgresRepoTestsFixture fixture) : IClassFixture<PostgresRepoTestsFixture>
{

	[Fact]
	public async Task ThenReturnsAllFlagsOrderedByName()
	{
		// Arrange
		var flag1 = TestHelpers.CreateTestFlag("z-flag", FlagEvaluationMode.Enabled);
		flag1.Name = "Z Flag";
		var flag2 = TestHelpers.CreateTestFlag("a-flag", FlagEvaluationMode.Disabled);
		flag2.Name = "A Flag";

		await fixture.Repository.CreateAsync(flag1);
		await fixture.Repository.CreateAsync(flag2);

		// Act
		var result = await fixture.Repository.GetAllAsync();

		// Assert
		result.Count.ShouldBeGreaterThanOrEqualTo(2);
		var createdFlags = result.Where(f => f.Key.Contains("-flag")).ToList();
		createdFlags[0].Name.ShouldBe("A Flag");
		createdFlags[1].Name.ShouldBe("Z Flag");
	}

	[Fact]
	public async Task If_NoFlags_ThenReturnsEmptyList()
	{
		// Arrange
		await fixture.ClearAllFlags();

		// Act
		var result = await fixture.Repository.GetAllAsync();

		// Assert
		result.ShouldNotBeNull();
		result.Count.ShouldBe(0);
	}
}

public class GetPagedAsync_WithValidParameters(PostgresRepoTestsFixture fixture) : IClassFixture<PostgresRepoTestsFixture>
{
	[Fact]
	public async Task ThenReturnsPaginatedResults()
	{
		// Arrange
		await fixture.ClearAllFlags();
		for (int i = 1; i <= 5; i++)
		{
			var flag = TestHelpers.CreateTestFlag($"page-flag-{i:00}", FlagEvaluationMode.Enabled);
			flag.Name = $"Page Flag {i:00}";
			await fixture.Repository.CreateAsync(flag);
		}

		// Act
		var result = await fixture.Repository.GetPagedAsync(1, 3);

		// Assert
		result.Items.Count.ShouldBe(3);
		result.TotalCount.ShouldBe(5);
		result.Page.ShouldBe(1);
		result.PageSize.ShouldBe(3);
		result.TotalPages.ShouldBe(2);
	}

	[Fact]
	public async Task If_InvalidPageParameters_ThenNormalizesValues()
	{
		// Act
		var result = await fixture.Repository.GetPagedAsync(-1, -5);

		// Assert
		result.Page.ShouldBe(1);
		result.PageSize.ShouldBe(10);
	}

	[Fact]
	public async Task If_PageSizeExceedsLimit_ThenCapsAt100()
	{
		// Act
		var result = await fixture.Repository.GetPagedAsync(1, 150);

		// Assert
		result.PageSize.ShouldBe(100);
	}
}

public class GetPagedAsync_WithFilter(PostgresRepoTestsFixture fixture) : IClassFixture<PostgresRepoTestsFixture>
{
	[Fact]
	public async Task If_FilterByEvaluationModes_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var enabledFlag = TestHelpers.CreateTestFlag("enabled-flag", FlagEvaluationMode.Enabled);
		var disabledFlag = TestHelpers.CreateTestFlag("disabled-flag", FlagEvaluationMode.Disabled);
		
		await fixture.Repository.CreateAsync(enabledFlag);
		await fixture.Repository.CreateAsync(disabledFlag);

		var filter = new FeatureFlagFilter { EvaluationModes = [FlagEvaluationMode.Enabled] };

		// Act
		var result = await fixture.Repository.GetPagedAsync(1, 10, filter);

		// Assert
		result.Items.All(f => f.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Enabled])).ShouldBeTrue();
		result.Items.Any(f => f.Key == "enabled-flag").ShouldBeTrue();
		result.Items.Any(f => f.Key == "disabled-flag").ShouldBeFalse();
	}

	[Fact]
	public async Task If_FilterByTags_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllFlags();
		var flag1 = TestHelpers.CreateTestFlag("tag-flag-1", FlagEvaluationMode.Enabled);
		flag1.Tags = new Dictionary<string, string> { { "team", "backend" } };
		var flag2 = TestHelpers.CreateTestFlag("tag-flag-2", FlagEvaluationMode.Enabled);
		flag2.Tags = new Dictionary<string, string> { { "team", "frontend" } };

		await fixture.Repository.CreateAsync(flag1);
		await fixture.Repository.CreateAsync(flag2);

		var filter = new FeatureFlagFilter 
		{ 
			Tags = new Dictionary<string, string> { { "team", "backend" } }
		};

		// Act
		var result = await fixture.Repository.GetPagedAsync(1, 10, filter);

		// Assert
		result.Items.Any(f => f.Key == "tag-flag-1").ShouldBeTrue();
		result.Items.Any(f => f.Key == "tag-flag-2").ShouldBeFalse();
	}

	[Fact]
	public async Task If_FilterByMultipleEvaluationModes_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllFlags();
		
		// Create flag with all specified modes
		var multiModeFlag = TestHelpers.CreateTestFlag("multi-mode-flag", FlagEvaluationMode.Disabled);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.TimeWindow);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserRolloutPercentage);
		multiModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.TenantRolloutPercentage);

		// Create flag with only some modes
		var partialModeFlag = TestHelpers.CreateTestFlag("partial-mode-flag", FlagEvaluationMode.Disabled);
		partialModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.Scheduled);
		partialModeFlag.EvaluationModeSet.AddMode(FlagEvaluationMode.UserTargeted);

		// Create flag with different modes
		var otherModeFlag = TestHelpers.CreateTestFlag("other-mode-flag", FlagEvaluationMode.Enabled);

		await fixture.Repository.CreateAsync(multiModeFlag);
		await fixture.Repository.CreateAsync(partialModeFlag);
		await fixture.Repository.CreateAsync(otherModeFlag);

		// Test filtering by single mode
		var singleModeFilter = new FeatureFlagFilter { EvaluationModes = [FlagEvaluationMode.UserTargeted] };
		var singleModeResult = await fixture.Repository.GetPagedAsync(1, 10, singleModeFilter);

		singleModeResult.Items.Any(f => f.Key == "multi-mode-flag").ShouldBeTrue();
		singleModeResult.Items.Any(f => f.Key == "partial-mode-flag").ShouldBeTrue();
		singleModeResult.Items.Any(f => f.Key == "other-mode-flag").ShouldBeFalse();

		// Test filtering by two modes
		var twoModeFilter = new FeatureFlagFilter 
		{ 
			EvaluationModes = [FlagEvaluationMode.Scheduled, FlagEvaluationMode.TimeWindow] 
		};
		var twoModeResult = await fixture.Repository.GetPagedAsync(1, 10, twoModeFilter);

		twoModeResult.Items.Any(f => f.Key == "multi-mode-flag").ShouldBeTrue();
		twoModeResult.Items.Any(f => f.Key == "partial-mode-flag").ShouldBeTrue(); // Has Scheduled
		twoModeResult.Items.Any(f => f.Key == "other-mode-flag").ShouldBeFalse();

		// Test filtering by all specified modes
		var allModeFilter = new FeatureFlagFilter 
		{ 
			EvaluationModes = [
				FlagEvaluationMode.Scheduled,
				FlagEvaluationMode.TimeWindow,
				FlagEvaluationMode.UserTargeted,
				FlagEvaluationMode.UserRolloutPercentage,
				FlagEvaluationMode.TenantRolloutPercentage
			] 
		};
		var allModeResult = await fixture.Repository.GetPagedAsync(1, 10, allModeFilter);

		allModeResult.Items.Any(f => f.Key == "multi-mode-flag").ShouldBeTrue();
		allModeResult.Items.Any(f => f.Key == "partial-mode-flag").ShouldBeTrue(); // Has some of the modes
		allModeResult.Items.Any(f => f.Key == "other-mode-flag").ShouldBeFalse();
	}
}

public class CreateAsync_WithValidFlag(PostgresRepoTestsFixture fixture) : IClassFixture<PostgresRepoTestsFixture>
{
	[Fact]
	public async Task ThenCreatesFlag()
	{
		// Arrange
		var flag = TestHelpers.CreateTestFlag("create-test", FlagEvaluationMode.Enabled);

		// Act
		var result = await fixture.Repository.CreateAsync(flag);

		// Assert
		result.ShouldBe(flag);
		var retrieved = await fixture.Repository.GetAsync("create-test");
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("create-test");
	}

	[Fact]
	public async Task If_FlagWithSchedule_ThenCreatesCorrectly()
	{
		// Arrange
		var flag = TestHelpers.CreateTestFlag("scheduled-flag", FlagEvaluationMode.Scheduled);
		DateTime startDt = DateTime.UtcNow.AddDays(1);
		DateTime endDt = DateTime.UtcNow.AddDays(7);
		flag.Schedule = FlagActivationSchedule.CreateSchedule(startDt, endDt);

		// Act
		await fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await fixture.Repository.GetAsync("scheduled-flag");
		retrieved.ShouldNotBeNull();

		// Account for PostgreSQL TIMESTAMPTZ microsecond precision loss
		retrieved.Schedule.ScheduledEnableDate.ShouldBeInRange(
			startDt.AddTicks(-10),
			startDt.AddTicks(10));

		retrieved.Schedule.ScheduledDisableDate.Value.ShouldBeInRange(
			endDt.AddTicks(-10),
			endDt.AddTicks(10));
	}

	[Fact]
	public async Task If_FlagWithOperationalWindow_ThenCreatesCorrectly()
	{
		// Arrange
		var flag = TestHelpers.CreateTestFlag("window-flag", FlagEvaluationMode.TimeWindow);
		flag.OperationalWindow = FlagOperationalWindow.CreateWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			"America/New_York",
			[DayOfWeek.Monday, DayOfWeek.Friday]);

		// Act
		await fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await fixture.Repository.GetAsync("window-flag");
		retrieved.ShouldNotBeNull();
		retrieved.OperationalWindow.WindowStartTime.ShouldBe(TimeSpan.FromHours(9));
		retrieved.OperationalWindow.TimeZone.ShouldBe("America/New_York");
		retrieved.OperationalWindow.WindowDays.ShouldContain(DayOfWeek.Monday);
	}
}

public class UpdateAsync_WithExistingFlag(PostgresRepoTestsFixture fixture) : IClassFixture<PostgresRepoTestsFixture>
{
	[Fact]
	public async Task ThenUpdatesFlag()
	{
		// Arrange
		var flag = TestHelpers.CreateTestFlag("update-test", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		flag.Name = "Updated Name";
		flag.Description = "Updated Description";
		flag.EvaluationModeSet.RemoveMode(FlagEvaluationMode.Enabled);
		flag.EvaluationModeSet.AddMode(FlagEvaluationMode.Disabled);

		// Act
		var result = await fixture.Repository.UpdateAsync(flag);

		// Assert
		result.ShouldBe(flag);
		var retrieved = await fixture.Repository.GetAsync("update-test");
		retrieved.ShouldNotBeNull();
		retrieved.Name.ShouldBe("Updated Name");
		retrieved.Description.ShouldBe("Updated Description");
		retrieved.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Disabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var flag = TestHelpers.CreateTestFlag("non-existent-update", FlagEvaluationMode.Enabled);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(() => fixture.Repository.UpdateAsync(flag));
	}
}

public class DeleteAsync_WhenFlagExists(PostgresRepoTestsFixture fixture) : IClassFixture<PostgresRepoTestsFixture>
{
	[Fact]
	public async Task ThenDeletesFlagAndReturnsTrue()
	{
		// Arrange
		var flag = TestHelpers.CreateTestFlag("delete-test", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.Repository.DeleteAsync("delete-test");

		// Assert
		result.ShouldBeTrue();
		var retrieved = await fixture.Repository.GetAsync("delete-test");
		retrieved.ShouldBeNull();
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsFalse()
	{
		// Act
		var result = await fixture.Repository.DeleteAsync("non-existent-delete");

		// Assert
		result.ShouldBeFalse();
	}
}