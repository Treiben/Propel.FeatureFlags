using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover ManageTenantAccessHandler integration scenarios:
 * Enabling tenants, disabling tenants, multiple tenants, non-existent flags, validation errors
 */

public class ManageTenantAccessHandler_EnableTenants(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_EnableSingleTenant_ThenAddsToAllowedTenants()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("tenant-enable-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var tenantIds = new List<string> { "tenant-123" };

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("tenant-enable-flag", tenantIds, true, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.UpdatedBy.ShouldBe("test-user");

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("tenant-enable-flag");
		updatedFlag.ShouldNotBeNull();
		updatedFlag.TenantAccess.AllowedTenants.ShouldContain("tenant-123");
		updatedFlag.AuditRecord.ModifiedBy.ShouldBe("test-user");
	}

	[Fact]
	public async Task If_EnableMultipleTenants_ThenAddsAllToAllowedTenants()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("multi-tenant-enable", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var tenantIds = new List<string> { "tenant-1", "tenant-2", "tenant-3" };

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("multi-tenant-enable", tenantIds, true, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("multi-tenant-enable");
		updatedFlag.TenantAccess.AllowedTenants.ShouldContain("tenant-1");
		updatedFlag.TenantAccess.AllowedTenants.ShouldContain("tenant-2");
		updatedFlag.TenantAccess.AllowedTenants.ShouldContain("tenant-3");
	}
}

public class ManageTenantAccessHandler_DisableTenants(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_DisableSingleTenant_ThenAddsToBlockedTenants()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("tenant-disable-flag", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var tenantIds = new List<string> { "blocked-tenant" };

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("tenant-disable-flag", tenantIds, false, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("tenant-disable-flag");
		updatedFlag.TenantAccess.BlockedTenants.ShouldContain("blocked-tenant");
	}

	[Fact]
	public async Task If_DisableMultipleTenants_ThenAddsAllToBlockedTenants()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("multi-tenant-disable", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var tenantIds = new List<string> { "blocked-1", "blocked-2" };

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("multi-tenant-disable", tenantIds, false, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify in repository
		var updatedFlag = await fixture.Repository.GetAsync("multi-tenant-disable");
		updatedFlag.TenantAccess.BlockedTenants.ShouldContain("blocked-1");
		updatedFlag.TenantAccess.BlockedTenants.ShouldContain("blocked-2");
	}
}

public class ManageTenantAccessHandler_NotFound(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_ThenReturnsNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var tenantIds = new List<string> { "tenant-123" };

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("non-existent-flag", tenantIds, true, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(404);
		problemResponse.ProblemDetails.Detail.ShouldContain("non-existent-flag");
		problemResponse.ProblemDetails.Detail.ShouldContain("was not found");
	}
}

public class ManageTenantAccessHandler_ValidationErrors(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_KeyIsEmpty_ThenReturnsBadRequest()
	{
		// Arrange
		var tenantIds = new List<string> { "tenant-123" };

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("", tenantIds, true, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("cannot be empty or null");
	}

	[Fact]
	public async Task If_EmptyTenantList_ThenReturnsBadRequest()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("empty-tenants-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var emptyTenantIds = new List<string>();

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("empty-tenants-flag", emptyTenantIds, true, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("At least one tenant ID must be provided");
	}

	[Fact]
	public async Task If_NullTenantList_ThenReturnsBadRequest()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("null-tenants-flag", FlagEvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("null-tenants-flag", null!, true, CancellationToken.None);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Detail.ShouldContain("At least one tenant ID must be provided");
	}
}

public class ManageTenantAccessHandler_CacheIntegration(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_FlagInCache_ThenRemovesFromCacheAfterTenantUpdate()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("cached-tenant-access", FlagEvaluationMode.Enabled);
		await fixture.Repository.CreateAsync(flag);

		// Add to cache
		if (fixture.Cache != null)
		{
			await fixture.Cache.SetAsync("cached-tenant-access", flag, TimeSpan.FromMinutes(5));
			var cachedFlag = await fixture.Cache.GetAsync("cached-tenant-access");
			cachedFlag.ShouldNotBeNull();
		}

		var tenantIds = new List<string> { "cache-tenant" };

		// Act
		var result = await fixture.ManageTenantAccessHandler.HandleAsync("cached-tenant-access", tenantIds, true, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<FeatureFlagResponse>>();

		// Verify cache was cleared
		if (fixture.Cache != null)
		{
			var cachedFlagAfterUpdate = await fixture.Cache.GetAsync("cached-tenant-access");
			cachedFlagAfterUpdate.ShouldBeNull();
		}
	}
}