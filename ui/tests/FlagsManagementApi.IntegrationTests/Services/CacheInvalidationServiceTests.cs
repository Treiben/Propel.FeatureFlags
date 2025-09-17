using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace FlagsManagementApi.IntegrationTests.Services;

public class CacheInvalidationService_InvalidateFlagAsync(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ApplicationScopedFlag_ThenInvalidatesApplicationCacheKey()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("app-cache-flag", EvaluationMode.Enabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		// Add flag to cache first
		var appCacheKey = new ApplicationCacheKey(flag.Key.Key, flag.Key.ApplicationName!, flag.Key.ApplicationVersion!);
		var evaluationCriteria = new EvaluationCriteria
		{
			FlagKey = flag.Key.Key,
			ActiveEvaluationModes = flag.ActiveEvaluationModes
		};
		await fixture.Cache.SetAsync(appCacheKey, evaluationCriteria);

		// Verify flag is in cache
		var cachedFlag = await fixture.Cache.GetAsync(appCacheKey);
		cachedFlag.ShouldNotBeNull();

		// Act
		await fixture.CacheInvalidationService.InvalidateFlagAsync(flag.Key, CancellationToken.None);

		// Assert - Flag should be removed from cache
		var invalidatedFlag = await fixture.Cache.GetAsync(appCacheKey);
		invalidatedFlag.ShouldBeNull();
	}

	[Fact]
	public async Task If_GlobalScopedFlag_ThenInvalidatesGlobalCacheKey()
	{
		// Arrange
		await fixture.ClearAllData();

		var globalFlagKey = new FlagKey("global-cache-flag", Scope.Global);
		var globalFlag = new FeatureFlag
		{
			Key = globalFlagKey,
			Name = "Global Cache Test Flag",
			Description = "Test flag for global cache invalidation",
			ActiveEvaluationModes = new EvaluationModes([EvaluationMode.Enabled]),
			Created = AuditTrail.FlagCreated("test-user")
		};
		await fixture.ManagementRepository.CreateAsync(globalFlag);

		// Add flag to cache first
		var globalCacheKey = new GlobalCacheKey(globalFlag.Key.Key);
		var evaluationCriteria = new EvaluationCriteria
		{
			FlagKey = globalFlag.Key.Key,
			ActiveEvaluationModes = globalFlag.ActiveEvaluationModes
		};
		await fixture.Cache.SetAsync(globalCacheKey, evaluationCriteria);

		// Verify flag is in cache
		var cachedFlag = await fixture.Cache.GetAsync(globalCacheKey);
		cachedFlag.ShouldNotBeNull();

		// Act
		await fixture.CacheInvalidationService.InvalidateFlagAsync(globalFlag.Key, CancellationToken.None);

		// Assert - Flag should be removed from cache
		var invalidatedFlag = await fixture.Cache.GetAsync(globalCacheKey);
		invalidatedFlag.ShouldBeNull();
	}

	[Fact]
	public async Task If_NoCacheConfigured_ThenDoesNotThrow()
	{
		// Arrange
		var nullCacheService = new CacheInvalidationService(null);
		var flagKey = new FlagKey("test-flag", Scope.Application, "test-app", "1.0");

		// Act & Assert - Should not throw when cache is null
		await Should.NotThrowAsync(async () => 
			await nullCacheService.InvalidateFlagAsync(flagKey, CancellationToken.None));
	}
}