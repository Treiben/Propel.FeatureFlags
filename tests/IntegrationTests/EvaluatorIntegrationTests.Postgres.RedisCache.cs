using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.PostgresSql;
using Propel.FeatureFlags.Redis;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FeatureFlags.IntegrationTests.Evaluator;

/* The tests cover these scenarios with RedisFeatureFlagCache and PostgresSQLFeatureFlagRepository implementations:
 *		All feature flag statuses (Enabled, Disabled, Scheduled, TimeWindow, UserTargeted, Percentage)
 *		Cache-first evaluation strategy
 *		Repository fallback when cache misses
 *		User overrides (enabled/disabled users)
 *		Flag expiration handling
 *		Targeting rule evaluation
 *		Percentage rollout with consistent hashing
 *		Time-based evaluations with timezone handling
 *		Variation retrieval with type conversion
 *		Error handling and edge cases
 *		Cancellation token support
*/

public class EvaluateAsync_WithEnabledFlag(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	private readonly FeatureFlagEvaluatorFixture _fixture = fixture;

	[Fact]
	public async Task ThenReturnsEnabled()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("enabled-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123"); 

		// Act
		var result = await _fixture.Evaluator.Evaluate("enabled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_FlagInCache_ThenUsesCache()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("cached-flag", FeatureFlagStatus.Enabled);
		
		// Only put in cache, not in repository
		await _fixture.Cache.SetAsync("cached-flag", flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _fixture.Evaluator.Evaluate("cached-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
	}

	[Fact]
	public async Task If_FlagNotInCacheButInRepository_ThenCachesFlag()
	{
		// Arrange
		await _fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("repo-flag", FeatureFlagStatus.Enabled);
		await _fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await _fixture.Evaluator.Evaluate("repo-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();

		// Verify flag is now cached
		var cachedFlag = await _fixture.Cache.GetAsync("repo-flag");
		cachedFlag.ShouldNotBeNull();
		cachedFlag.Key.ShouldBe("repo-flag");
	}
}

public class EvaluateAsync_WithDisabledFlag(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("disabled-flag", FeatureFlagStatus.Disabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("disabled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag disabled");
	}
}

public class EvaluateAsync_WithNonExistentFlag(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task ThenReturnsFlagNotFound()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("non-existent", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag not found, using default disabled flag");
	}
}

public class EvaluateAsync_WithExpiredFlag(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task ThenReturnsExpired()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("expired-flag", FeatureFlagStatus.Enabled);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(-1); // Expired yesterday
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("expired-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldContain("Flag expired");
	}
}

public class EvaluateAsync_WithUserOverrides(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_UserExplicitlyEnabled_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("override-flag", FeatureFlagStatus.Disabled);
		flag.EnabledUsers = ["user123", "user456"];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("override-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}

	[Fact]
	public async Task If_UserExplicitlyDisabled_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("override-flag", FeatureFlagStatus.Enabled);
		flag.DisabledUsers = ["user123", "user456"];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("override-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("User explicitly disabled");
	}
}

public class EvaluateAsync_WithScheduledFlag(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_BeforeEnableDate_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("scheduled-flag", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddDays(1);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("scheduled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_AfterEnableBeforeDisable_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("scheduled-flag", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(-1);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddHours(1);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("scheduled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_AfterDisableDate_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("scheduled-flag", FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(-2);
		flag.ScheduledDisableDate = DateTime.UtcNow.AddHours(-1);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("scheduled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Scheduled disable date passed");
	}
}

public class EvaluateAsync_WithTimeWindowFlag(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_WithinTimeWindow_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("time-window-flag", FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.TimeZone = "UTC";
		await fixture.Repository.CreateAsync(flag);

		// Use 12 PM UTC as evaluation time
		var noonUtc = DateTime.UtcNow.Date.AddHours(12);
		var context = new EvaluationContext(userId: "user123", evaluationTime: noonUtc, timeZone: "UTC");

		// Act
		var result = await fixture.Evaluator.Evaluate("time-window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_OutsideTimeWindow_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("time-window-flag", FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.TimeZone = "UTC";
		await fixture.Repository.CreateAsync(flag);

		// Use 8 AM UTC as evaluation time (before window)
		var earlyMorning = DateTime.UtcNow.Date.AddHours(8);
		var context = new EvaluationContext(userId: "user123", evaluationTime: earlyMorning, timeZone: "UTC");

		// Act
		var result = await fixture.Evaluator.Evaluate("time-window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task If_OutsideAllowedDays_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("time-window-flag", FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9);
		flag.WindowEndTime = TimeSpan.FromHours(17);
		flag.WindowDays = [DayOfWeek.Monday, DayOfWeek.Tuesday];
		flag.TimeZone = "UTC";
		await fixture.Repository.CreateAsync(flag);

		// Find a Wednesday and set evaluation time to noon
		var wednesday = DateTime.UtcNow.Date;
		while (wednesday.DayOfWeek != DayOfWeek.Wednesday)
		{
			wednesday = wednesday.AddDays(1);
		}
		wednesday = wednesday.AddHours(12); // Noon on Wednesday

		var context = new EvaluationContext(userId: "user123", evaluationTime: wednesday, timeZone: "UTC");

		// Act
		var result = await fixture.Evaluator.Evaluate("time-window-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Outside allowed days");
	}
}

public class EvaluateAsync_WithUserTargetedFlag(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_TargetingRuleMatches_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("targeted-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = ["US", "CA"],
				Variation = "north-america"
			}
		];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "region", "US" } });

		// Act
		var result = await fixture.Evaluator.Evaluate("targeted-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("north-america");
		result.Reason.ShouldContain("Targeting rule matched");
	}

	[Fact]
	public async Task If_NoTargetingRuleMatches_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("targeted-flag", FeatureFlagStatus.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "region",
				Operator = TargetingOperator.In,
				Values = ["US", "CA"],
				Variation = "north-america"
			}
		];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "region", "EU" } });

		// Act
		var result = await fixture.Evaluator.Evaluate("targeted-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class EvaluateAsync_WithPercentageFlag(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_NoUserId_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 50;
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: null); // No UserId

		// Act
		var result = await fixture.Evaluator.Evaluate("percentage-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("User ID required for percentage rollout");
	}

	[Fact]
	public async Task If_SameUserIdAlwaysGivesSameResult()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("percentage-flag", FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 50;
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "consistent-user");

		// Act - Multiple evaluations
		var result1 = await fixture.Evaluator.Evaluate("percentage-flag", context);
		var result2 = await fixture.Evaluator.Evaluate("percentage-flag", context);
		var result3 = await fixture.Evaluator.Evaluate("percentage-flag", context);

		// Assert - All results should be the same
		result2.IsEnabled.ShouldBe(result1.IsEnabled);
		result3.IsEnabled.ShouldBe(result1.IsEnabled);
		result2.Variation.ShouldBe(result1.Variation);
		result3.Variation.ShouldBe(result1.Variation);
	}
}

public class GetVariationAsync_WithVariations(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_FlagEnabledWithVariation_ThenReturnsVariationValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("variation-flag", FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("variation-flag", "default", context);

		// Assert
		result.ShouldBe("premium-features");
	}

	[Fact]
	public async Task If_FlagDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("variation-flag", FeatureFlagStatus.Disabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("variation-flag", "default-value", context);

		// Assert
		result.ShouldBe("default-value");
	}

	[Fact]
	public async Task If_ComplexObjectVariation_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("config-flag", FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", new { MaxItems = 100, EnableAdvanced = true } }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("config-flag", new { MaxItems = 10, EnableAdvanced = false }, context);

		// Assert
		result.ShouldNotBeNull();
		result.MaxItems.ShouldBe(100);
		result.EnableAdvanced.ShouldBeTrue();
	}

	[Fact]
	public async Task If_FlagNotFound_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var context = new EvaluationContext(userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("non-existent", 42, context);

		// Assert
		result.ShouldBe(42);
	}
}

public class FeatureFlagEvaluator_CancellationToken(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_CancellationRequested_ThenOperationsCancelled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("cancel-test", FeatureFlagStatus.Enabled);
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123");

		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Evaluator.Evaluate("cancel-test", context, cts.Token));

		await Should.ThrowAsync<OperationCanceledException>(
			() => fixture.Evaluator.GetVariation("cancel-test", "default", context, cts.Token));
	}
}

public class EvaluateAsync_WithTenantOverrides(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_TenantExplicitlyDisabled_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-disabled-flag", FeatureFlagStatus.Enabled);
		flag.DisabledTenants = ["blocked-tenant", "another-blocked"];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "blocked-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-disabled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Fact]
	public async Task If_TenantExplicitlyEnabled_ThenContinuesEvaluation()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-enabled-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = ["premium-tenant", "enterprise-tenant"];
		flag.TenantPercentageEnabled = 0; // Would normally block all tenants
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "premium-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-enabled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // Continues to flag evaluation (Enabled status)
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_TenantDisabledTakesPrecedenceOverEnabled_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-precedence-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = ["conflict-tenant"];
		flag.DisabledTenants = ["conflict-tenant"]; // Same tenant in both lists
		flag.TenantPercentageEnabled = 100; // Would allow tenant
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "conflict-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-precedence-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Tenant explicitly disabled"); // Disabled takes precedence
	}

	[Fact]
	public async Task If_NoTenantId_ThenSkipsTenantEvaluation()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("no-tenant-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 0; // Would block if tenant evaluation ran
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(userId: "user123"); // No tenant ID

		// Act
		var result = await fixture.Evaluator.Evaluate("no-tenant-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // Skips tenant evaluation, goes to flag status
		result.Reason.ShouldBe("Flag enabled");
	}
}

public class EvaluateAsync_WithTenantPercentageRollout(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_TenantPercentage0_ThenAllTenantsBlocked()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-percentage-0-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 0; // Block all tenants
		await fixture.Repository.CreateAsync(flag);

		var tenantIds = new[] { "tenant1", "tenant2", "tenant3", "tenant4", "tenant5" };

		foreach (var tenantId in tenantIds)
		{
			var context = new EvaluationContext(tenantId: tenantId, userId: "user123");

			// Act
			var result = await fixture.Evaluator.Evaluate("tenant-percentage-0-flag", context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeFalse();
			result.Variation.ShouldBe("off");
			result.Reason.ShouldBe("Tenant not in percentage rollout");
		}
	}

	[Fact]
	public async Task If_TenantPercentage100_ThenAllTenantsContinue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-percentage-100-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 100; // Allow all tenants
		await fixture.Repository.CreateAsync(flag);

		var tenantIds = new[] { "tenant1", "tenant2", "tenant3", "tenant4", "tenant5" };

		foreach (var tenantId in tenantIds)
		{
			var context = new EvaluationContext(tenantId: tenantId, userId: "user123");

			// Act
			var result = await fixture.Evaluator.Evaluate("tenant-percentage-100-flag", context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeTrue(); // Continues to flag evaluation
			result.Variation.ShouldBe("on");
			result.Reason.ShouldBe("Flag enabled");
		}
	}

	[Fact]
	public async Task If_SameTenantIdAlwaysGivesSameResult()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-consistency-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 50;
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "consistent-tenant", userId: "user123");

		// Act - Multiple evaluations
		var result1 = await fixture.Evaluator.Evaluate("tenant-consistency-flag", context);
		var result2 = await fixture.Evaluator.Evaluate("tenant-consistency-flag", context);
		var result3 = await fixture.Evaluator.Evaluate("tenant-consistency-flag", context);

		// Assert - All results should be the same
		result2.IsEnabled.ShouldBe(result1.IsEnabled);
		result3.IsEnabled.ShouldBe(result1.IsEnabled);
		result2.Variation.ShouldBe(result1.Variation);
		result3.Variation.ShouldBe(result1.Variation);
		result2.Reason.ShouldBe(result1.Reason);
		result3.Reason.ShouldBe(result1.Reason);
	}

	[Fact]
	public async Task If_DifferentTenantsSameFlag_ThenMayHaveDifferentResults()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-distribution-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 50; // 50% rollout
		await fixture.Repository.CreateAsync(flag);

		var tenant1Context = new EvaluationContext(tenantId: "tenant-alpha", userId: "user123");
		var tenant2Context = new EvaluationContext(tenantId: "tenant-beta", userId: "user123");

		// Act
		var result1 = await fixture.Evaluator.Evaluate("tenant-distribution-flag", tenant1Context);
		var result2 = await fixture.Evaluator.Evaluate("tenant-distribution-flag", tenant2Context);

		// Assert - Results may be different due to different tenant IDs in hash
		result1.ShouldNotBeNull();
		result2.ShouldNotBeNull();

		// Both should be valid responses (either enabled with flag status or disabled by tenant percentage)
		(result1.IsEnabled && result1.Reason == "Flag enabled" ||
		 !result1.IsEnabled && result1.Reason == "Tenant not in percentage rollout").ShouldBeTrue();

		(result2.IsEnabled && result2.Reason == "Flag enabled" ||
		 !result2.IsEnabled && result2.Reason == "Tenant not in percentage rollout").ShouldBeTrue();
	}
}

public class EvaluateAsync_WithTenantAndUserCombinations(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_TenantAllowedButUserDisabled_ThenReturnsUserDisabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-user-combo-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = ["allowed-tenant"];
		flag.DisabledUsers = ["blocked-user"];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "allowed-tenant", userId: "blocked-user");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-user-combo-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("User explicitly disabled");
	}

	[Fact]
	public async Task If_TenantDisabled_ThenUserOverrideIgnored()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-blocks-user-flag", FeatureFlagStatus.Enabled);
		flag.DisabledTenants = ["blocked-tenant"];
		flag.EnabledUsers = ["vip-user"]; // User is enabled but tenant is blocked
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "blocked-tenant", userId: "vip-user");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-blocks-user-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Tenant explicitly disabled"); // Tenant evaluation comes first
	}

	[Fact]
	public async Task If_TenantAllowedAndUserEnabled_ThenReturnsUserEnabled()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-user-both-enabled-flag", FeatureFlagStatus.Disabled);
		flag.EnabledTenants = ["premium-tenant"];
		flag.EnabledUsers = ["vip-user"];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "premium-tenant", userId: "vip-user");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-user-both-enabled-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}

	[Fact]
	public async Task If_TenantInPercentageUserInPercentage_ThenBothEvaluated()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-user-percentage-flag", FeatureFlagStatus.Percentage);
		flag.TenantPercentageEnabled = 100; // Allow all tenants
		flag.PercentageEnabled = 50; // 50% user rollout
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "test-tenant", userId: "test-user");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-user-percentage-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldStartWith("User percentage rollout");
		// Result depends on user hash, but tenant should have been allowed to continue
	}
}

public class EvaluateAsync_WithTenantVariations(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_TenantDisabled_ThenReturnsDefaultVariation()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-variation-flag", FeatureFlagStatus.Enabled);
		flag.DisabledTenants = ["blocked-tenant"];
		flag.DefaultVariation = "tenant-blocked";
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "blocked-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-variation-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("tenant-blocked");
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Fact]
	public async Task If_TenantAllowedFlagEnabled_ThenReturnsOnVariation()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-allowed-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = ["premium-tenant"];
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "premium-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-allowed-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_TenantNotInPercentage_ThenReturnsDefaultVariation()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-percentage-blocked-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 0; // Block all tenants
		flag.DefaultVariation = "percentage-blocked";
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "any-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.Evaluate("tenant-percentage-blocked-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("percentage-blocked");
		result.Reason.ShouldBe("Tenant not in percentage rollout");
	}
}

public class GetVariationAsync_WithTenantScenarios(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_TenantAllowedFlagEnabledWithVariation_ThenReturnsVariationValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-variation-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = ["premium-tenant"];
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "premium-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("tenant-variation-flag", "default", context);

		// Assert
		result.ShouldBe("premium-features");
	}

	[Fact]
	public async Task If_TenantDisabled_ThenReturnsDefaultValue()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-blocked-variation-flag", FeatureFlagStatus.Enabled);
		flag.DisabledTenants = ["blocked-tenant"];
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium-features" },
			{ "off", "basic-features" }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "blocked-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("tenant-blocked-variation-flag", "default-value", context);

		// Assert
		result.ShouldBe("default-value");
	}

	[Fact]
	public async Task If_TenantConfigurationObject_ThenDeserializesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("tenant-config-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = ["enterprise-tenant"];
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", new { MaxUsers = 1000, SupportLevel = "enterprise" } }
		};
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "enterprise-tenant", userId: "user123");

		// Act
		var result = await fixture.Evaluator.GetVariation("tenant-config-flag", new { MaxUsers = 10, SupportLevel = "basic" }, context);

		// Assert
		result.ShouldNotBeNull();
		result.MaxUsers.ShouldBe(1000);
		result.SupportLevel.ShouldBe("enterprise");
	}
}

public class EvaluateAsync_WithTenantEdgeCases(FeatureFlagEvaluatorFixture fixture) : IClassFixture<FeatureFlagEvaluatorFixture>
{
	[Fact]
	public async Task If_EmptyTenantId_ThenSkipsTenantEvaluation()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("empty-tenant-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 0; // Would block if tenant evaluation ran
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "", userId: "user123"); // Empty tenant ID

		// Act
		var result = await fixture.Evaluator.Evaluate("empty-tenant-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // Skips tenant evaluation
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_WhitespaceTenantId_ThenSkipsTenantEvaluation()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("whitespace-tenant-flag", FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 0; // Would block if tenant evaluation ran
		await fixture.Repository.CreateAsync(flag);

		var context = new EvaluationContext(tenantId: "   ", userId: "user123"); // Whitespace tenant ID

		// Act
		var result = await fixture.Evaluator.Evaluate("whitespace-tenant-flag", context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // Skips tenant evaluation
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_TenantWithSpecialCharacters_ThenEvaluatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("special-tenant-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = ["tenant@company.com", "tenant-123", "tenant_456"];
		await fixture.Repository.CreateAsync(flag);

		var contexts = new[]
		{
			new EvaluationContext(tenantId: "tenant@company.com", userId: "user123"),
			new EvaluationContext(tenantId: "tenant-123", userId: "user123"),
			new EvaluationContext(tenantId: "tenant_456", userId: "user123")
		};

		foreach (var context in contexts)
		{
			// Act
			var result = await fixture.Evaluator.Evaluate("special-tenant-flag", context);

			// Assert
			result.ShouldNotBeNull();
			result.IsEnabled.ShouldBeTrue(); // All should be enabled
			result.Reason.ShouldBe("Flag enabled");
		}
	}

	[Fact]
	public async Task If_CaseSensitiveTenantIds_ThenMatchesExactly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = FeatureFlagEvaluatorFixture.CreateTestFlag("case-sensitive-tenant-flag", FeatureFlagStatus.Enabled);
		flag.EnabledTenants = ["TenantABC"];
		await fixture.Repository.CreateAsync(flag);

		var correctCaseContext = new EvaluationContext(tenantId: "TenantABC", userId: "user123");
		var wrongCaseContext = new EvaluationContext(tenantId: "tenantabc", userId: "user123");

		// Act
		var correctResult = await fixture.Evaluator.Evaluate("case-sensitive-tenant-flag", correctCaseContext);
		var wrongResult = await fixture.Evaluator.Evaluate("case-sensitive-tenant-flag", wrongCaseContext);

		// Assert
		correctResult.ShouldNotBeNull();
		correctResult.IsEnabled.ShouldBeTrue();
		correctResult.Reason.ShouldBe("Flag enabled");

		wrongResult.ShouldNotBeNull();
		wrongResult.IsEnabled.ShouldBeTrue(); // Not in enabled list, but flag status is Enabled
		wrongResult.Reason.ShouldBe("Flag enabled");
	}
}

public class FeatureFlagEvaluatorFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public FeatureFlagEvaluator Evaluator { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public IFlagEvaluationHandler EvaluationHandler { get; private set; } = null!;
	
	private ConnectionMultiplexer _redisConnection = null!;
	private readonly ILogger<PostgreSQLFeatureFlagRepository> _repositoryLogger;
	private readonly ILogger<RedisFeatureFlagCache> _cacheLogger;

	public FeatureFlagEvaluatorFixture()
	{
		_postgresContainer = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("featureflags")
			.WithUsername("postgres")
			.WithPassword("postgres")
			.WithPortBinding(5432, true)
			.Build();

		_redisContainer = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithPortBinding(6379, true)
			.Build();

		_repositoryLogger = new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object;
		_cacheLogger = new Mock<ILogger<RedisFeatureFlagCache>>().Object;
	}

	public async Task InitializeAsync()
	{
		// Start containers
		await _postgresContainer.StartAsync();
		await _redisContainer.StartAsync();

		// Setup PostgreSQL
		var postgresConnectionString = _postgresContainer.GetConnectionString();
		await CreatePostgresTables(postgresConnectionString);
		Repository = new PostgreSQLFeatureFlagRepository(postgresConnectionString, _repositoryLogger);

		// Setup Redis
		var redisConnectionString = _redisContainer.GetConnectionString();
		_redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
		Cache = new RedisFeatureFlagCache(_redisConnection, _cacheLogger);

		// Setup Evaluation Handler
		EvaluationHandler = EvaluatorChainBuilder.BuildChain();

		// Setup Evaluator
		Evaluator = new FeatureFlagEvaluator(Repository, EvaluationHandler, Cache);
	}

	public async Task DisposeAsync()
	{
		_redisConnection?.Dispose();
		await _postgresContainer.DisposeAsync();
		await _redisContainer.DisposeAsync();
	}

	private static async Task CreatePostgresTables(string connectionString)
	{
		const string createTableSql = @"
		CREATE TABLE feature_flags (
			key VARCHAR(255) PRIMARY KEY,
			name VARCHAR(500) NOT NULL,
			description TEXT NOT NULL,
			status INTEGER NOT NULL,
			created_at TIMESTAMP NOT NULL,
			updated_at TIMESTAMP NOT NULL,
			created_by VARCHAR(255) NOT NULL,
			updated_by VARCHAR(255) NOT NULL,
			expiration_date TIMESTAMP NULL,
			scheduled_enable_date TIMESTAMP NULL,
			scheduled_disable_date TIMESTAMP NULL,
			window_start_time TIME NULL,
			window_end_time TIME NULL,
			time_zone VARCHAR(100) NULL,
			window_days JSONB NOT NULL DEFAULT '[]',
			percentage_enabled INTEGER NOT NULL DEFAULT 0,
			targeting_rules JSONB NOT NULL DEFAULT '[]',
			enabled_users JSONB NOT NULL DEFAULT '[]',
			disabled_users JSONB NOT NULL DEFAULT '[]',
			enabled_tenants JSONB NOT NULL DEFAULT '[]',
			disabled_tenants JSONB NOT NULL DEFAULT '[]',
			tenant_percentage_enabled INTEGER NOT NULL DEFAULT 0,
			variations JSONB NOT NULL DEFAULT '{}',
			default_variation VARCHAR(255) NOT NULL DEFAULT 'off',
			tags JSONB NOT NULL DEFAULT '{}',
			is_permanent BOOLEAN NOT NULL DEFAULT false
		);

		CREATE INDEX ix_feature_flags_status ON feature_flags(status);
		CREATE INDEX ix_feature_flags_expiration_date ON feature_flags(expiration_date) WHERE expiration_date IS NOT NULL;
		CREATE INDEX ix_feature_flags_created_at ON feature_flags(created_at);
		CREATE INDEX ix_feature_flags_tags ON feature_flags USING GIN(tags);
	";

		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand(createTableSql, connection);
		await command.ExecuteNonQueryAsync();
	}

	public static FeatureFlag CreateTestFlag(string key, FeatureFlagStatus status)
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
			TargetingRules = [],
			EnabledUsers = [],
			DisabledUsers = [],
			EnabledTenants = [],
			DisabledTenants = [],
			TenantPercentageEnabled = 0,
			Variations = [],
			Tags = [],
			WindowDays = [],
			PercentageEnabled = 0,
			IsPermanent = false
		};
	}

	// Helper method to clear all data between tests
	public async Task ClearAllData()
	{
		// Clear PostgreSQL
		var connectionString = _postgresContainer.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();

		// Clear Redis
		await Cache.ClearAsync();
	}
}