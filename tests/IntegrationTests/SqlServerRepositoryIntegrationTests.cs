using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.SqlServer;
using Testcontainers.MsSql;

namespace FeatureFlags.IntegrationTests.SqlServer;

/* The tests cover these scenarios:
 *		CRUD operations with various flag configurations
 *		Complex data serialization/deserialization
 *		Time-based functionality
 *		Tag-based queries
 *		Error handling and edge cases
 *		Cancellation token support
 *		Database-specific error scenarios
*/

public class CreateAsync_WithValidFlag : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public CreateAsync_WithValidFlag(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ThenCreatesFlag()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("create-test", FeatureFlagStatus.Enabled);

		// Act
		var result = await _fixture.Repository.CreateAsync(flag);

		// Assert
		result.ShouldBe(flag);

		// Verify it's actually in the database
		var retrieved = await _fixture.Repository.GetAsync("create-test");
		retrieved.ShouldNotBeNull();
		retrieved.Key.ShouldBe("create-test");
	}

	[Fact]
	public async Task If_FlagWithTimeData_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("time-flag", FeatureFlagStatus.TimeWindow);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(30);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(7);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.TimeZone = "America/New_York";

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("time-flag");
		retrieved.ShouldNotBeNull();
		retrieved.ExpirationDate.ShouldNotBeNull();
		retrieved.ExpirationDate.Value.ShouldBeInRange(flag.ExpirationDate.Value.AddSeconds(-1), flag.ExpirationDate.Value.AddSeconds(1));
		retrieved.ScheduledEnableDate.ShouldNotBeNull();
		retrieved.ScheduledEnableDate.Value.ShouldBeInRange(flag.ScheduledEnableDate.Value.AddSeconds(-1), flag.ScheduledEnableDate.Value.AddSeconds(1));
		retrieved.ScheduledDisableDate.ShouldNotBeNull();
		retrieved.ScheduledDisableDate.Value.ShouldBeInRange(flag.ScheduledDisableDate.Value.AddSeconds(-1), flag.ScheduledDisableDate.Value.AddSeconds(1));
		retrieved.WindowStartTime.ShouldBe(flag.WindowStartTime);
		retrieved.WindowEndTime.ShouldBe(flag.WindowEndTime);
		retrieved.TimeZone.ShouldBe("America/New_York");
	}

	[Fact]
	public async Task If_FlagWithPercentageRollout_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 75;

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("percentage-flag");
		retrieved.ShouldNotBeNull();
		retrieved.PercentageEnabled.ShouldBe(75);
	}
}

public class CreateAsync_WithTenantData : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public CreateAsync_WithTenantData(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagWithTenantOverrides_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("tenant-overrides-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = new List<string> { "tenant1", "tenant2", "premium-tenant" };
		flag.DisabledTenants = new List<string> { "blocked-tenant", "test-tenant" };
		flag.TenantPercentageEnabled = 75;

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("tenant-overrides-flag");
		retrieved.ShouldNotBeNull();
		retrieved.EnabledTenants.ShouldContain("tenant1");
		retrieved.EnabledTenants.ShouldContain("tenant2");
		retrieved.EnabledTenants.ShouldContain("premium-tenant");
		retrieved.DisabledTenants.ShouldContain("blocked-tenant");
		retrieved.DisabledTenants.ShouldContain("test-tenant");
		retrieved.TenantPercentageEnabled.ShouldBe(75);
	}

	[Fact]
	public async Task If_FlagWithEmptyTenantLists_ThenStoresEmptyLists()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("empty-tenant-lists-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = new List<string>();
		flag.DisabledTenants = new List<string>();
		flag.TenantPercentageEnabled = 0;

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("empty-tenant-lists-flag");
		retrieved.ShouldNotBeNull();
		retrieved.EnabledTenants.ShouldNotBeNull();
		retrieved.EnabledTenants.Count.ShouldBe(0);
		retrieved.DisabledTenants.ShouldNotBeNull();
		retrieved.DisabledTenants.Count.ShouldBe(0);
		retrieved.TenantPercentageEnabled.ShouldBe(0);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(25)]
	[InlineData(50)]
	[InlineData(75)]
	[InlineData(100)]
	public async Task If_FlagWithDifferentTenantPercentages_ThenStoresCorrectly(int tenantPercentage)
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag($"tenant-percentage-{tenantPercentage}-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = tenantPercentage;

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync($"tenant-percentage-{tenantPercentage}-flag");
		retrieved.ShouldNotBeNull();
		retrieved.TenantPercentageEnabled.ShouldBe(tenantPercentage);
	}
}

public class When_FlagExists : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public When_FlagExists(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_UpdateWithBasicData_ThenUpdatesFlag()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("update-test", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		var originalUpdatedAt = flag.UpdatedAt;
		await Task.Delay(10); // Ensure time difference

		flag.Status = FeatureFlagStatus.Enabled;
		flag.Name = "Updated Name";
		flag.Description = "Updated Description";
		flag.UpdatedBy = "updater";

		// Act
		var result = await _fixture.Repository.UpdateAsync(flag);

		// Assert
		result.ShouldBe(flag);
		result.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);

		// Verify changes in database
		var retrieved = await _fixture.Repository.GetAsync("update-test");
		retrieved.ShouldNotBeNull();
		retrieved.Status.ShouldBe(FeatureFlagStatus.Enabled);
		retrieved.Name.ShouldBe("Updated Name");
		retrieved.Description.ShouldBe("Updated Description");
		retrieved.UpdatedBy.ShouldBe("updater");
		retrieved.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
	}

	[Fact]
	public async Task If_UpdateWithFlagComplexData_ThenStoresCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("complex-update", FeatureFlagStatus.UserTargeted);
		await _fixture.Repository.CreateAsync(flag);

		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US", "CA", "UK" },
				Variation = "region-specific"
			}
		};
		flag.EnabledUsers = new List<string> { "admin1", "admin2" };
		flag.Variations = new Dictionary<string, object>
		{
			{ "region-specific", new { currency = "USD", language = "en" } }
		};

		// Act
		await _fixture.Repository.UpdateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("complex-update");
		retrieved.ShouldNotBeNull();
		retrieved.TargetingRules.Count.ShouldBe(1);
		retrieved.TargetingRules[0].Attribute.ShouldBe("region");
		retrieved.TargetingRules[0].Values.ShouldContain("US");
		retrieved.TargetingRules[0].Values.ShouldContain("CA");
		retrieved.TargetingRules[0].Values.ShouldContain("UK");
		retrieved.EnabledUsers.ShouldContain("admin1");
		retrieved.EnabledUsers.ShouldContain("admin2");
		retrieved.Variations.ShouldContainKey("region-specific");
	}

	[Fact]
	public async Task If_Delete_ThenDeletesFlagAndReturnsTrue()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("delete-test", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.DeleteAsync("delete-test");

		// Assert
		result.ShouldBeTrue();

		// Verify it's actually deleted
		var retrieved = await _fixture.Repository.GetAsync("delete-test");
		retrieved.ShouldBeNull();
	}

	[Fact]
	public async Task If_GetWhenFlagEnabled_ThenReturnsFlagWithEnabledStatus()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("test-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetAsync("test-flag");

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("test-flag");
		result.Name.ShouldBe(flag.Name);
		result.Status.ShouldBe(FeatureFlagStatus.Enabled);
		result.CreatedBy.ShouldBe(flag.CreatedBy);
		result.UpdatedBy.ShouldBe(flag.UpdatedBy);
	}

	[Fact]
	public async Task If_GetWhenFlagDisabled_ThenReturnsFlagWithDisabledStatus()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("test-flag", FeatureFlagStatus.Disabled);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetAsync("test-flag");

		// Assert
		result.ShouldNotBeNull();
		result.Key.ShouldBe("test-flag");
		result.Name.ShouldBe(flag.Name);
		result.Status.ShouldBe(FeatureFlagStatus.Disabled);
		result.CreatedBy.ShouldBe(flag.CreatedBy);
		result.UpdatedBy.ShouldBe(flag.UpdatedBy);
	}

	[Fact]
	public async Task If_GetAndFlagHasComplexData_ThenDeserializesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("complex-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "userType",
				Operator = TargetingOperator.Equals,
				Values = new List<string> { "premium", "enterprise" },
				Variation = "premium-variation"
			}
		};
		flag.EnabledUsers = new List<string> { "user1", "user2" };
		flag.DisabledUsers = new List<string> { "user3", "user4" };
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "enabled-value" },
			{ "off", "disabled-value" },
			{ "premium", new { feature = "advanced", limit = 1000 } }
		};
		flag.Tags = new Dictionary<string, string>
		{
			{ "team", "platform" },
			{ "environment", "production" }
		};
		flag.WindowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday };

		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Repository.GetAsync("complex-flag");

		// Assert
		result.ShouldNotBeNull();
		result.TargetingRules.Count.ShouldBe(1);
		result.TargetingRules[0].Attribute.ShouldBe("userType");
		result.TargetingRules[0].Operator.ShouldBe(TargetingOperator.Equals);
		result.TargetingRules[0].Values.ShouldContain("premium");
		result.TargetingRules[0].Values.ShouldContain("enterprise");

		result.EnabledUsers.ShouldContain("user1");
		result.EnabledUsers.ShouldContain("user2");
		result.DisabledUsers.ShouldContain("user3");
		result.DisabledUsers.ShouldContain("user4");

		result.Variations.ShouldContainKey("on");
		result.Variations.ShouldContainKey("premium");

		result.Tags.ShouldContainKeyAndValue("team", "platform");
		result.Tags.ShouldContainKeyAndValue("environment", "production");

		result.WindowDays.ShouldContain(DayOfWeek.Monday);
		result.WindowDays.ShouldContain(DayOfWeek.Friday);
	}

	[Fact]
	public async Task If_GetAll_ThenReturnsAllFlags()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("flag-1", FeatureFlagStatus.Enabled);
		flag1.Name = "Alpha Flag";
		var flag2 = _fixture.CreateTestFlag("flag-2", FeatureFlagStatus.Disabled);
		flag2.Name = "Beta Flag";
		var flag3 = _fixture.CreateTestFlag("flag-3", FeatureFlagStatus.Scheduled);
		flag3.Name = "Gamma Flag";

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Repository.GetAllAsync();

		// Assert
		result.Count.ShouldBe(3);
		// Should be ordered by name
		result[0].Name.ShouldBe("Alpha Flag");
		result[1].Name.ShouldBe("Beta Flag");
		result[2].Name.ShouldBe("Gamma Flag");
	}

	[Fact]
	public async Task If_GetAllWithNoFlags_ThenReturnsEmptyList()
	{
		// Arrange
		await _fixture.ClearAllFlags();

		// Act
		var result = await _fixture.Repository.GetAllAsync();

		// Assert
		result.ShouldNotBeNull();
		result.Count.ShouldBe(0);
	}
}

public class When_FlagExistsWithTenantData : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public When_FlagExistsWithTenantData(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_UpdateTenantLists_ThenStoresUpdatedData()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("update-tenant-lists", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = new List<string> { "original-tenant" };
		flag.DisabledTenants = new List<string> { "original-blocked" };
		flag.TenantPercentageEnabled = 50;
		await _fixture.Repository.CreateAsync(flag);

		// Update tenant data
		flag.EnabledTenants = new List<string> { "updated-tenant1", "updated-tenant2" };
		flag.DisabledTenants = new List<string> { "updated-blocked1", "updated-blocked2", "updated-blocked3" };
		flag.TenantPercentageEnabled = 80;

		// Act
		await _fixture.Repository.UpdateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("update-tenant-lists");
		retrieved.ShouldNotBeNull();
		retrieved.EnabledTenants.Count.ShouldBe(2);
		retrieved.EnabledTenants.ShouldContain("updated-tenant1");
		retrieved.EnabledTenants.ShouldContain("updated-tenant2");
		retrieved.EnabledTenants.ShouldNotContain("original-tenant");
		
		retrieved.DisabledTenants.Count.ShouldBe(3);
		retrieved.DisabledTenants.ShouldContain("updated-blocked1");
		retrieved.DisabledTenants.ShouldContain("updated-blocked2");
		retrieved.DisabledTenants.ShouldContain("updated-blocked3");
		retrieved.DisabledTenants.ShouldNotContain("original-blocked");
		
		retrieved.TenantPercentageEnabled.ShouldBe(80);
	}

	[Fact]
	public async Task If_ClearTenantLists_ThenStoresEmptyLists()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("clear-tenant-lists", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = new List<string> { "tenant1", "tenant2" };
		flag.DisabledTenants = new List<string> { "blocked1", "blocked2" };
		flag.TenantPercentageEnabled = 60;
		await _fixture.Repository.CreateAsync(flag);

		// Clear tenant data
		flag.EnabledTenants = new List<string>();
		flag.DisabledTenants = new List<string>();
		flag.TenantPercentageEnabled = 0;

		// Act
		await _fixture.Repository.UpdateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("clear-tenant-lists");
		retrieved.ShouldNotBeNull();
		retrieved.EnabledTenants.Count.ShouldBe(0);
		retrieved.DisabledTenants.Count.ShouldBe(0);
		retrieved.TenantPercentageEnabled.ShouldBe(0);
	}

	[Fact]
	public async Task If_GetComplexFlagWithTenantData_ThenDeserializesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("complex-tenant-flag", FeatureFlagStatus.UserTargeted);
		flag.EnabledTenants = new List<string> { "premium-tenant", "enterprise-tenant" };
		flag.DisabledTenants = new List<string> { "blocked-tenant-1", "blocked-tenant-2" };
		flag.TenantPercentageEnabled = 40;
		
		// Also add other complex data to ensure tenant data works alongside existing features
		flag.EnabledUsers = new List<string> { "admin1", "admin2" };
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US", "CA" },
				Variation = "north-america"
			}
		};

		await _fixture.Repository.CreateAsync(flag);

		// Act
		var retrieved = await _fixture.Repository.GetAsync("complex-tenant-flag");

		// Assert
		retrieved.ShouldNotBeNull();
		
		// Verify tenant data
		retrieved.EnabledTenants.Count.ShouldBe(2);
		retrieved.EnabledTenants.ShouldContain("premium-tenant");
		retrieved.EnabledTenants.ShouldContain("enterprise-tenant");
		
		retrieved.DisabledTenants.Count.ShouldBe(2);
		retrieved.DisabledTenants.ShouldContain("blocked-tenant-1");
		retrieved.DisabledTenants.ShouldContain("blocked-tenant-2");
		
		retrieved.TenantPercentageEnabled.ShouldBe(40);

		// Verify other data still works
		retrieved.EnabledUsers.Count.ShouldBe(2);
		retrieved.TargetingRules.Count.ShouldBe(1);
		retrieved.TargetingRules[0].Attribute.ShouldBe("region");
	}
}

public class GetAllAsync_WithTenantData : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public GetAllAsync_WithTenantData(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_MultipleFlagsWithTenantData_ThenReturnsAllWithCorrectTenantData()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		
		var flag1 = _fixture.CreateTestFlag("tenant-flag-1", FeatureFlagStatus.Enabled);
		flag1.Name = "Alpha Tenant Flag";
		flag1.EnabledTenants = new List<string> { "tenant-alpha" };
		flag1.TenantPercentageEnabled = 25;

		var flag2 = _fixture.CreateTestFlag("tenant-flag-2", FeatureFlagStatus.Enabled);
		flag2.Name = "Beta Tenant Flag";
		flag2.DisabledTenants = new List<string> { "blocked-beta" };
		flag2.TenantPercentageEnabled = 75;

		var flag3 = _fixture.CreateTestFlag("tenant-flag-3", FeatureFlagStatus.Enabled);
		flag3.Name = "Gamma Tenant Flag";
		flag3.EnabledTenants = new List<string> { "vip1", "vip2" };
		flag3.DisabledTenants = new List<string> { "blocked1", "blocked2" };
		flag3.TenantPercentageEnabled = 100;

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Repository.GetAllAsync();

		// Assert
		result.Count.ShouldBe(3);
		
		// Ordered by name, so Alpha, Beta, Gamma
		var alphaFlag = result.First(f => f.Name == "Alpha Tenant Flag");
		alphaFlag.EnabledTenants.ShouldContain("tenant-alpha");
		alphaFlag.TenantPercentageEnabled.ShouldBe(25);

		var betaFlag = result.First(f => f.Name == "Beta Tenant Flag");
		betaFlag.DisabledTenants.ShouldContain("blocked-beta");
		betaFlag.TenantPercentageEnabled.ShouldBe(75);

		var gammaFlag = result.First(f => f.Name == "Gamma Tenant Flag");
		gammaFlag.EnabledTenants.Count.ShouldBe(2);
		gammaFlag.DisabledTenants.Count.ShouldBe(2);
		gammaFlag.TenantPercentageEnabled.ShouldBe(100);
	}
}

public class TenantData_EdgeCases : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public TenantData_EdgeCases(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_TenantIdWithSpecialCharacters_ThenStoresAndRetrievesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("special-chars-tenant", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = new List<string> 
		{ 
			"tenant@company.com", 
			"tenant-with-dashes", 
			"tenant_with_underscores",
			"tenant with spaces",
			"tenant!@#$%^&*()",
		};
		flag.DisabledTenants = new List<string> 
		{ 
			"blocked@evil.com", 
			"blocked-tenant-123" 
		};

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("special-chars-tenant");
		retrieved.ShouldNotBeNull();
		retrieved.EnabledTenants.ShouldContain("tenant@company.com");
		retrieved.EnabledTenants.ShouldContain("tenant-with-dashes");
		retrieved.EnabledTenants.ShouldContain("tenant_with_underscores");
		retrieved.EnabledTenants.ShouldContain("tenant with spaces");
		retrieved.EnabledTenants.ShouldContain("tenant!@#$%^&*()");
		retrieved.DisabledTenants.ShouldContain("blocked@evil.com");
		retrieved.DisabledTenants.ShouldContain("blocked-tenant-123");
	}

	[Fact]
	public async Task If_DuplicateTenantsInLists_ThenStoresAndRetrievesAllEntries()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("duplicate-tenants", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = new List<string> { "tenant1", "tenant1", "tenant2", "tenant1" };
		flag.DisabledTenants = new List<string> { "blocked", "blocked", "other-blocked" };

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("duplicate-tenants");
		retrieved.ShouldNotBeNull();
		// Should preserve duplicates as stored
		retrieved.EnabledTenants.Count.ShouldBe(4);
		retrieved.EnabledTenants.Count(t => t == "tenant1").ShouldBe(3);
		retrieved.DisabledTenants.Count.ShouldBe(3);
		retrieved.DisabledTenants.Count(t => t == "blocked").ShouldBe(2);
	}

	[Fact]
	public async Task If_VeryLongTenantLists_ThenStoresAndRetrievesCorrectly()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("large-tenant-lists", FeatureFlagStatus.Enabled);
		
		// Create large lists
		flag.EnabledTenants = Enumerable.Range(1, 1000).Select(i => $"enabled-tenant-{i}").ToList();
		flag.DisabledTenants = Enumerable.Range(1, 500).Select(i => $"blocked-tenant-{i}").ToList();
		flag.TenantPercentageEnabled = 42;

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync("large-tenant-lists");
		retrieved.ShouldNotBeNull();
		retrieved.EnabledTenants.Count.ShouldBe(1000);
		retrieved.EnabledTenants.ShouldContain("enabled-tenant-1");
		retrieved.EnabledTenants.ShouldContain("enabled-tenant-500");
		retrieved.EnabledTenants.ShouldContain("enabled-tenant-1000");
		
		retrieved.DisabledTenants.Count.ShouldBe(500);
		retrieved.DisabledTenants.ShouldContain("blocked-tenant-1");
		retrieved.DisabledTenants.ShouldContain("blocked-tenant-250");
		retrieved.DisabledTenants.ShouldContain("blocked-tenant-500");
		
		retrieved.TenantPercentageEnabled.ShouldBe(42);
	}

	[Theory]
	[InlineData(-10)] // Negative percentage
	[InlineData(150)] // Over 100%
	[InlineData(0)]   // Zero percentage
	[InlineData(100)] // Max percentage
	public async Task If_EdgeCaseTenantPercentages_ThenStoresCorrectly(int percentage)
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag($"edge-percentage-{Math.Abs(percentage)}", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = percentage;

		// Act
		await _fixture.Repository.CreateAsync(flag);

		// Assert
		var retrieved = await _fixture.Repository.GetAsync($"edge-percentage-{Math.Abs(percentage)}");
		retrieved.ShouldNotBeNull();
		retrieved.TenantPercentageEnabled.ShouldBe(percentage);
	}
}

public class SqlServerFeatureFlagRepository_CancellationToken : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public SqlServerFeatureFlagRepository_CancellationToken(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_CancellationRequested_ThenOperationsCancelled()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag = _fixture.CreateTestFlag("cancellation-test", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Repository.GetAsync("cancellation-test", cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Repository.GetAllAsync(cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => _fixture.Repository.CreateAsync(flag, cts.Token));
	}
}

public class SqlServerFeatureFlagRepository_DatabaseErrors : IClassFixture<SqlServerFeatureFlagRepositoryFixture>
{
	private readonly SqlServerFeatureFlagRepositoryFixture _fixture;

	public SqlServerFeatureFlagRepository_DatabaseErrors(SqlServerFeatureFlagRepositoryFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_DuplicateKey_ThenThrowsSqlException()
	{
		// Arrange
		await _fixture.ClearAllFlags();
		var flag1 = _fixture.CreateTestFlag("duplicate-key", FeatureFlagStatus.Enabled);
		var flag2 = _fixture.CreateTestFlag("duplicate-key", FeatureFlagStatus.Disabled);

		await _fixture.Repository.CreateAsync(flag1);

		// Act & Assert
		await Should.ThrowAsync<SqlException>(
			() => _fixture.Repository.CreateAsync(flag2));
	}

	[Fact]
	public async Task If_InvalidConnectionString_ThenThrowsSqlException()
	{
		// Arrange
		var logger = new Mock<ILogger<SqlServerFeatureFlagRepository>>();
		var invalidRepository = new SqlServerFeatureFlagRepository("invalid-connection-string", logger.Object);
		var flag = _fixture.CreateTestFlag("test", FeatureFlagStatus.Enabled);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => invalidRepository.CreateAsync(flag));
	}
}

public class SqlServerFeatureFlagRepositoryFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _container;
	public SqlServerFeatureFlagRepository Repository { get; private set; } = null!;
	private readonly ILogger<SqlServerFeatureFlagRepository> _logger;

	public SqlServerFeatureFlagRepositoryFixture()
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithPassword("StrongP@ssw0rd!")
			.WithEnvironment("ACCEPT_EULA", "Y")
			.WithEnvironment("SA_PASSWORD", "StrongP@ssw0rd!")
			.WithPortBinding(1433, true)
			.Build();

		_logger = new Mock<ILogger<SqlServerFeatureFlagRepository>>().Object;
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();
		var connectionString = _container.GetConnectionString();
		
		// Create the repository after container is started
		await CreateDatabase(connectionString);
		await CreateTables(connectionString);
		
		// Initialize repository with the connection string
		Repository = new SqlServerFeatureFlagRepository(connectionString, _logger);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	private async Task CreateDatabase(string connectionString)
	{
		// The testcontainer already creates the database, so we just need to ensure it's accessible
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		// Database is ready
	}

	private async Task CreateTables(string connectionString)
	{
		const string createTableSql = @"
		CREATE TABLE feature_flags (
			[key] NVARCHAR(255) PRIMARY KEY,
			[name] NVARCHAR(500) NOT NULL,
			[description] NVARCHAR(MAX) NOT NULL,
			[status] INT NOT NULL,
			created_at DATETIME2 NOT NULL,
			updated_at DATETIME2 NOT NULL,
			created_by NVARCHAR(255) NOT NULL,
			updated_by NVARCHAR(255) NOT NULL,
			expiration_date DATETIME2 NULL,
			scheduled_enable_date DATETIME2 NULL,
			scheduled_disable_date DATETIME2 NULL,
			window_start_time TIME NULL,
			window_end_time TIME NULL,
			time_zone NVARCHAR(100) NULL,
			window_days NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			percentage_enabled INT NOT NULL DEFAULT 0,
			targeting_rules NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			enabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			disabled_users NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			enabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			disabled_tenants NVARCHAR(MAX) NOT NULL DEFAULT '[]',
			tenant_percentage_enabled INT NOT NULL DEFAULT 0,
			variations NVARCHAR(MAX) NOT NULL DEFAULT '{}',
			default_variation NVARCHAR(255) NOT NULL DEFAULT 'off',
			tags NVARCHAR(MAX) NOT NULL DEFAULT '{}',
			is_permanent BIT NOT NULL DEFAULT 0
		);

		CREATE INDEX IX_feature_flags_status ON feature_flags([status]);
		CREATE INDEX IX_feature_flags_expiration_date ON feature_flags(expiration_date) WHERE expiration_date IS NOT NULL;
		CREATE INDEX IX_feature_flags_created_at ON feature_flags(created_at);
	";

	using var connection = new SqlConnection(connectionString);
	await connection.OpenAsync();
	using var command = new SqlCommand(createTableSql, connection);
	await command.ExecuteNonQueryAsync();
	}

	public FeatureFlag CreateTestFlag(string key, FeatureFlagStatus status)
	{
		return new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			Status = status,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			CreatedBy = "integration-test",
			UpdatedBy = "integration-test",
			DefaultVariation = "off",
			TargetingRules = new List<TargetingRule>(),
			EnabledUsers = new List<string>(),
			DisabledUsers = new List<string>(),
			Variations = new Dictionary<string, object>(),
			Tags = new Dictionary<string, string>(),
			WindowDays = new List<DayOfWeek>(),
			PercentageEnabled = 0,
			IsPermanent = false
		};
	}

	// Helper method to clear data between tests
	public async Task ClearAllFlags()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new SqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();
	}
}