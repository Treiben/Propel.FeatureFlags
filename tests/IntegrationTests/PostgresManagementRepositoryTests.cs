using FeatureFlags.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace FeatureFlags.IntegrationTests.Postgres;

public class GetAsync_WhenFlagExists(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task ThenReturnsFlag()
	{
		// Arrange
		var (flag, _) = TestHelpers.SetupTestCases("get-test", EvaluationMode.Enabled);

		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.ManagementRepository.GetAsync(flag.Key);

		// Assert
		result.ShouldNotBeNull();
		result.Key.Key.ShouldBe("get-test");
		result.Name.ShouldBe(flag.Name);
		result.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagWithComplexData_ThenDeserializesCorrectly()
	{
		// Arrange
		var (flag, _) = TestHelpers.SetupTestCases("complex-flag", EvaluationMode.UserTargeted);
		flag.TargetingRules =
		[
			TargetingRuleFactory.CreateTargetingRule(
				attribute: "region",
				op: TargetingOperator.In,
				values: ["US", "CA"],
				variation: "region-specific"
			)
		];
		flag.UserAccessControl = new AccessControl(allowed: ["user1", "user2"]);
		flag.Tags = new Dictionary<string, string> { { "team", "platform" }, { "env", "test" } };

		await fixture.ManagementRepository.CreateAsync(flag);

		// Act
		var result = await fixture.ManagementRepository.GetAsync(flag.Key);

		// Assert
		result.ShouldNotBeNull();

		result.TargetingRules.Count.ShouldBe(1);
		result.TargetingRules.ShouldBeOfType<List<ITargetingRule>>();
		result.Tags.ShouldContainKeyAndValue("team", "platform");

		var stringRule = (StringTargetingRule)result.TargetingRules[0];
		stringRule.Attribute.ShouldBe("region");
		stringRule.Values.ShouldContain("US");
	}
}

public class GetAllAsync_WithMultipleFlags(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{

	[Fact]
	public async Task ThenReturnsAllFlagsOrderedByName()
	{
		// Arrange
		var (flag1, _) = TestHelpers.SetupTestCases("z-flag", EvaluationMode.Enabled);
		flag1.Name = "Z Flag";
		var (flag2, _) = TestHelpers.SetupTestCases("a-flag", EvaluationMode.Disabled);
		flag2.Name = "A Flag";

		await fixture.ManagementRepository.CreateAsync(flag1);
		await fixture.ManagementRepository.CreateAsync(flag2);

		// Act
		var result = await fixture.ManagementRepository.GetAllAsync();

		// Assert
		result.Count.ShouldBeGreaterThanOrEqualTo(2);
		var createdFlags = result.Where(f => f.Key.Key.Contains("-flag")).ToList();
		createdFlags[0].Name.ShouldBe("A Flag");
		createdFlags[1].Name.ShouldBe("Z Flag");
	}

	[Fact]
	public async Task If_NoFlags_ThenReturnsEmptyList()
	{
		// Arrange
		await fixture.ClearAllData();

		// Act
		var result = await fixture.ManagementRepository.GetAllAsync();

		// Assert
		result.ShouldNotBeNull();
		result.Count.ShouldBe(0);
	}
}

public class GetPagedAsync_WithValidParameters(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task ThenReturnsPaginatedResults()
	{
		// Arrange
		await fixture.ClearAllData();
		for (int i = 1; i <= 5; i++)
		{
			var (flag, _) = TestHelpers.SetupTestCases($"page-flag-{i:00}", EvaluationMode.Enabled);
			flag.Name = $"Page Flag {i:00}";
			await fixture.ManagementRepository.CreateAsync(flag);
		}

		// Act
		var result = await fixture.ManagementRepository.GetPagedAsync(1, 3);

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
		var result = await fixture.ManagementRepository.GetPagedAsync(-1, -5);

		// Assert
		result.Page.ShouldBe(1);
		result.PageSize.ShouldBe(1);
	}

	[Fact]
	public async Task If_PageSizeExceedsLimit_ThenCapsAt100()
	{
		// Act
		var result = await fixture.ManagementRepository.GetPagedAsync(1, 150);

		// Assert
		result.PageSize.ShouldBe(100);
	}
}

public class GetPagedAsync_WithFilter(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task If_FilterByEvaluationModes_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllData();
		var (enabledFlag, _) = TestHelpers.SetupTestCases("enabled-flag", EvaluationMode.Enabled);
		var (disabledFlag, _) = TestHelpers.SetupTestCases("disabled-flag", EvaluationMode.Disabled);
		
		await fixture.ManagementRepository.CreateAsync(enabledFlag);
		await fixture.ManagementRepository.CreateAsync(disabledFlag);

		var filter = new FeatureFlagFilter { EvaluationModes = [EvaluationMode.Enabled] };

		// Act
		var result = await fixture.ManagementRepository.GetPagedAsync(1, 10, filter);

		// Assert
		result.Items.All(f => f.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled])).ShouldBeTrue();
		result.Items.Any(f => f.Key.Key == "enabled-flag").ShouldBeTrue();
		result.Items.Any(f => f.Key.Key == "disabled-flag").ShouldBeFalse();
	}

	[Fact]
	public async Task If_FilterByTags_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllData();
		var (flag1, _) = TestHelpers.SetupTestCases("tag-flag-1", EvaluationMode.Enabled);
		flag1.Tags = new Dictionary<string, string> { { "team", "backend" } };

		var (flag2, _) = TestHelpers.SetupTestCases("tag-flag-2", EvaluationMode.Enabled);
		flag2.Tags = new Dictionary<string, string> { { "team", "frontend" } };

		await fixture.ManagementRepository.CreateAsync(flag1);
		await fixture.ManagementRepository.CreateAsync(flag2);

		var filter = new FeatureFlagFilter 
		{ 
			Tags = new Dictionary<string, string> { { "team", "backend" } }
		};

		// Act
		var result = await fixture.ManagementRepository.GetPagedAsync(1, 10, filter);

		// Assert
		result.Items.Any(f => f.Key.Key == "tag-flag-1").ShouldBeTrue();
		result.Items.Any(f => f.Key.Key == "tag-flag-2").ShouldBeFalse();
	}

	[Fact]
	public async Task If_FilterByMultipleEvaluationModes_ThenReturnsMatchingFlags()
	{
		// Arrange
		await fixture.ClearAllData();
		
		// Create flag with all specified modes
		var (multiModeFlag, _) = TestHelpers.SetupTestCases("multi-mode-flag", EvaluationMode.Disabled);
		multiModeFlag.ActiveEvaluationModes.AddMode(EvaluationMode.Scheduled);
		multiModeFlag.ActiveEvaluationModes.AddMode(EvaluationMode.TimeWindow);
		multiModeFlag.ActiveEvaluationModes.AddMode(EvaluationMode.UserTargeted);
		multiModeFlag.ActiveEvaluationModes.AddMode(EvaluationMode.UserRolloutPercentage);
		multiModeFlag.ActiveEvaluationModes.AddMode(EvaluationMode.TenantRolloutPercentage);

		// Create flag with only some modes
		var (partialModeFlag, _) = TestHelpers.SetupTestCases("partial-mode-flag", EvaluationMode.Disabled);
		partialModeFlag.ActiveEvaluationModes.AddMode(EvaluationMode.Scheduled);
		partialModeFlag.ActiveEvaluationModes.AddMode(EvaluationMode.UserTargeted);

		// Create flag with different modes
		var (otherModeFlag, _) = TestHelpers.SetupTestCases("other-mode-flag", EvaluationMode.Enabled);

		await fixture.ManagementRepository.CreateAsync(multiModeFlag);
		await fixture.ManagementRepository.CreateAsync(partialModeFlag);
		await fixture.ManagementRepository.CreateAsync(otherModeFlag);

		// Test filtering by single mode
		var singleModeFilter = new FeatureFlagFilter { EvaluationModes = [EvaluationMode.UserTargeted] };
		var singleModeResult = await fixture.ManagementRepository.GetPagedAsync(1, 10, singleModeFilter);

		singleModeResult.Items.Any(f => f.Key.Key == "multi-mode-flag").ShouldBeTrue();
		singleModeResult.Items.Any(f => f.Key.Key == "partial-mode-flag").ShouldBeTrue();
		singleModeResult.Items.Any(f => f.Key.Key == "other-mode-flag").ShouldBeFalse();

		// Test filtering by two modes
		var twoModeFilter = new FeatureFlagFilter 
		{ 
			EvaluationModes = [EvaluationMode.Scheduled, EvaluationMode.TimeWindow] 
		};
		var twoModeResult = await fixture.ManagementRepository.GetPagedAsync(1, 10, twoModeFilter);

		twoModeResult.Items.Any(f => f.Key.Key == "multi-mode-flag").ShouldBeTrue();
		twoModeResult.Items.Any(f => f.Key.Key == "partial-mode-flag").ShouldBeTrue(); // Has Scheduled
		twoModeResult.Items.Any(f => f.Key.Key == "other-mode-flag").ShouldBeFalse();

		// Test filtering by all specified modes
		var allModeFilter = new FeatureFlagFilter 
		{ 
			EvaluationModes = [
				EvaluationMode.Scheduled,
				EvaluationMode.TimeWindow,
				EvaluationMode.UserTargeted,
				EvaluationMode.UserRolloutPercentage,
				EvaluationMode.TenantRolloutPercentage
			] 
		};
		var allModeResult = await fixture.ManagementRepository.GetPagedAsync(1, 10, allModeFilter);

		allModeResult.Items.Any(f => f.Key.Key == "multi-mode-flag").ShouldBeTrue();
		allModeResult.Items.Any(f => f.Key.Key == "partial-mode-flag").ShouldBeTrue(); // Has some of the modes
		allModeResult.Items.Any(f => f.Key.Key == "other-mode-flag").ShouldBeFalse();
	}
}

public class CreateAsync_WithValidFlag(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task ThenCreatesFlag()
	{
		// Arrange
		var (flag, _) = TestHelpers.SetupTestCases("create-test", EvaluationMode.Enabled);

		// Act
		var result = await fixture.ManagementRepository.CreateAsync(flag);

		// Assert
		result.ShouldBe(flag);
		var retrieved = await fixture.ManagementRepository.GetAsync(flag.Key);
		retrieved.ShouldNotBeNull();
		retrieved.Key.Key.ShouldBe("create-test");
	}

	[Fact]
	public async Task If_FlagWithSchedule_ThenCreatesCorrectly()
	{
		// Arrange
		var (flag, _) = TestHelpers.SetupTestCases("scheduled-flag", EvaluationMode.Scheduled);
		DateTime startDt = DateTime.UtcNow.AddDays(1);
		DateTime endDt = DateTime.UtcNow.AddDays(7);
		flag.Schedule = ActivationSchedule.CreateSchedule(startDt, endDt);

		// Act
		await fixture.ManagementRepository.CreateAsync(flag);

		// Assert
		var retrieved = await fixture.ManagementRepository.GetAsync(flag.Key);
		retrieved.ShouldNotBeNull();

		// Account for PostgreSQL TIMESTAMPTZ microsecond precision loss
		retrieved.Schedule.EnableOn.ShouldBeInRange(
			startDt.AddTicks(-10),
			startDt.AddTicks(10));

		retrieved.Schedule.DisableOn.ShouldBeInRange(
			endDt.AddTicks(-10),
			endDt.AddTicks(10));
	}

	[Fact]
	public async Task If_FlagWithOperationalWindow_ThenCreatesCorrectly()
	{
		// Arrange
		var (flag, _) = TestHelpers.SetupTestCases("window-flag", EvaluationMode.TimeWindow);
		flag.OperationalWindow = new OperationalWindow(
			TimeSpan.FromHours(9),
			TimeSpan.FromHours(17),
			"America/New_York",
			[DayOfWeek.Monday, DayOfWeek.Friday]);

		// Act
		await fixture.ManagementRepository.CreateAsync(flag);

		// Assert
		var retrieved = await fixture.ManagementRepository.GetAsync(flag.Key);
		retrieved.ShouldNotBeNull();
		retrieved.OperationalWindow.StartOn.ShouldBe(TimeSpan.FromHours(9));
		retrieved.OperationalWindow.TimeZone.ShouldBe("America/New_York");
		retrieved.OperationalWindow.DaysActive.ShouldContain(DayOfWeek.Monday);
	}
}

public class UpdateAsync_WithExistingFlag(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task ThenUpdatesFlag()
	{
		// Arrange
		var (flag, _) = TestHelpers.SetupTestCases("update-test", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		flag.Name = "Updated Name";
		flag.Description = "Updated Description";
		flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.Enabled);
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.Disabled);

		// Act
		var result = await fixture.ManagementRepository.UpdateAsync(flag);

		// Assert
		result.ShouldBe(flag);

		var retrieved = await fixture.ManagementRepository.GetAsync(flag.Key);

		retrieved.ShouldNotBeNull();
		retrieved.Name.ShouldBe("Updated Name");
		retrieved.Description.ShouldBe("Updated Description");
		retrieved.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagDoesNotExist_ThenThrowsInvalidOperationException()
	{
		// Arrange
		var (flag, _) = TestHelpers.SetupTestCases("non-existent-update", EvaluationMode.Enabled);

		// Act & Assert
		await Should.ThrowAsync<FlagUpdateException>(() => fixture.ManagementRepository.UpdateAsync(flag));
	}
}

public class DeleteAsync_WhenFlagExists(PostgresRepositoriesTests fixture) : IClassFixture<PostgresRepositoriesTests>
{
	[Fact]
	public async Task ThenDeletesFlagAndReturnsTrue()
	{
		// Arrange
		var (flag, _) = TestHelpers.SetupTestCases("delete-test", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		// Act

		var result = await fixture.ManagementRepository.DeleteAsync(flag.Key, "test-user", "Testing repository deletion method");

		// Assert
		result.ShouldBeTrue();
		var retrieved = await fixture.ManagementRepository.GetAsync(flag.Key);
		retrieved.ShouldBeNull();
	}
}