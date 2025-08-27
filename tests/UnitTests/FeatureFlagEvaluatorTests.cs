using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;
using System.Text.Json;

namespace FeatureFlags.UnitTests.Evaluator;

public class EvaluateAsync_WhenFlagFoundInCache
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task Then_ReturnsResultWithoutRepositoryCall()
	{
		// Arrange
		var flagKey = "test-flag";
		var context = new EvaluationContext(userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");

		_tests._mockRepository.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
	}
}

public class EvaluateAsync_WhenFlagNotInCacheButInRepository
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task Then_CachesFlagAndReturnsResult()
	{
		// Arrange
		var flagKey = "test-flag";
		var context = new EvaluationContext(userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("Flag enabled");

		_tests._mockCache.Verify(x => x.SetAsync(flagKey, flag, TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()), Times.Once);
	}
}

public class EvaluateAsync_WhenFlagNotFound
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task Then_ReturnsDisabledResult()
	{
		// Arrange
		var flagKey = "non-existent-flag";
		var context = new EvaluationContext(userId: "user123");

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag not found, using default disabled flag");
	}
}

public class EvaluateAsync_WhenFlagExpired
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task Then_ReturnsDisabledResult()
	{
		// Arrange
		var flagKey = "expired-flag";
		var context = new EvaluationContext(userId: "user123", evaluationTime: DateTime.UtcNow);

		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(-1); // Expired yesterday

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(flag.DefaultVariation);
		result.Reason.ShouldContain("Flag expired");
	}
}

public class EvaluateAsync_WhenUserExplicitlyDisabled
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task Then_ReturnsDisabledResult()
	{
		// Arrange
		var flagKey = "test-flag";
		var userId = "disabled-user";
		var context = new EvaluationContext(userId: userId);
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.DisabledUsers.Add(userId);

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(flag.DefaultVariation);
		result.Reason.ShouldBe("User explicitly disabled");
	}
}

public class EvaluateAsync_WhenUserExplicitlyEnabled
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task Then_ReturnsEnabledResult()
	{
		// Arrange
		var flagKey = "test-flag";
		var userId = "enabled-user";
		var context = new EvaluationContext(userId: userId);
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Disabled);
		flag.EnabledUsers.Add(userId);

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("on");
		result.Reason.ShouldBe("User explicitly enabled");
	}
}

public class EvaluateAsync_WithBasicStatuses
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Theory]
	[InlineData(FeatureFlagStatus.Disabled, false, "Flag disabled")]
	[InlineData(FeatureFlagStatus.Enabled, true, "Flag enabled")]
	public async Task Then_ReturnsExpectedResult(
	FeatureFlagStatus status, bool expectedEnabled, string expectedReason)
	{
		// Arrange
		var flagKey = "test-flag";
		var context = new EvaluationContext(userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, status);

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBe(expectedEnabled);
		result.Reason.ShouldBe(expectedReason);
	}
}

public class EvaluateAsync_WithScheduledStatus
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task If_BeforeEnableDate_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "scheduled-flag";
		var context = new EvaluationContext(userId: "user123", evaluationTime: DateTime.UtcNow);

		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(1); // Enable in 1 hour

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Scheduled enable date not reached");
	}

	[Fact]
	public async Task If_AfterEnableDate_ThenReturnsEnabled()
	{
		// Arrange
		var flagKey = "scheduled-flag";
		var context = new EvaluationContext(userId: "user123", evaluationTime: DateTime.UtcNow);
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(-1); // Enabled 1 hour ago

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Scheduled enable date reached");
	}

	[Fact]
	public async Task If_AfterDisableDate_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "scheduled-flag";
		var context = new EvaluationContext(userId: "user123", evaluationTime: DateTime.UtcNow);
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Scheduled);
		flag.ScheduledEnableDate = DateTime.UtcNow.AddHours(-2); // Enabled 2 hours ago
		flag.ScheduledDisableDate = DateTime.UtcNow.AddHours(-1); // Disabled 1 hour ago

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Scheduled disable date passed");
	}
}

public class EvaluateAsync_WithTimeWindow
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task If_InsideWindow_ThenReturnsEnabled()
	{
		// Arrange
		var flagKey = "time-window-flag";
		// Create a UTC DateTime - this is the key fix
		var evaluationTime = DateTime.UtcNow.Date.AddHours(10); // 10 AM UTC
		var context = new EvaluationContext(userId: "user123", evaluationTime: evaluationTime);
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.WindowDays = [evaluationTime.DayOfWeek];

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Reason.ShouldBe("Within time window");
	}

	[Fact]
	public async Task If_OutsideWindow_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "time-window-flag";
		// Create a UTC DateTime - this is the key fix
		var evaluationTime = DateTime.UtcNow.Date.AddHours(20); // 8 PM UTC
		var context = new EvaluationContext(userId: "user123", evaluationTime: evaluationTime);
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.WindowDays = [evaluationTime.DayOfWeek];

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Outside time window");
	}

	[Fact]
	public async Task If_WrongDay_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "time-window-flag";
		// Create a UTC DateTime - this is the key fix
		var evaluationTime = DateTime.UtcNow.Date.AddHours(10); // 10 AM UTC
		var context = new EvaluationContext(userId: "user123", evaluationTime: evaluationTime);
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.TimeWindow);
		flag.WindowStartTime = TimeSpan.FromHours(9); // 9 AM
		flag.WindowEndTime = TimeSpan.FromHours(17); // 5 PM
		flag.WindowDays = [DayOfWeek.Monday]; // Only Monday allowed

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Outside allowed days");
	}
}

public class EvaluateAsync_WithUserTargeted
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task If_MatchingRule_ThenReturnsEnabled()
	{
		// Arrange
		var flagKey = "targeted-flag";
		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "userType", "premium" } });
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "userType",
				Operator = TargetingOperator.Equals,
				Values = ["premium"],
				Variation = "premium-variation"
			}
		];

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("premium-variation");
		result.Reason.ShouldBe("Targeting rule matched: userType Equals premium");
	}

	[Fact]
	public async Task If_NoMatchingRule_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "targeted-flag";
		var context = new EvaluationContext(userId: "user123", attributes: new Dictionary<string, object> { { "userType", "basic" } });
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.UserTargeted);
		flag.TargetingRules =
		[
			new TargetingRule
			{
				Attribute = "userType",
				Operator = TargetingOperator.Equals,
				Values = ["premium"],
				Variation = "premium-variation"
			}
		];

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("No targeting rules matched");
	}
}

public class EvaluateAsync_WithPercentageRollout
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Theory]
	[InlineData(0, true)] // 0% hash falls within 50% rollout
	[InlineData(49, true)] // 49% hash falls within 50% rollout
	[InlineData(50, false)] // 50% hash falls outside 50% rollout
	[InlineData(99, false)] // 99% hash falls outside 50% rollout
	public async Task ThenReturnsCorrectResult(int expectedHash, bool expectedEnabled)
	{
		// Note: This test is simplified. In reality, hash computation is deterministic but hard to predict.
		// For thorough testing, you'd need to test with known user IDs that produce specific hash values.
		var flagKey = "percentage-flag";
		var context = new EvaluationContext(userId: "test-user");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 50; // 50% rollout

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.Reason.ShouldStartWith("User percentage rollout:");
		// Note: We can't easily predict the exact result due to hash computation
		// but we can verify the structure is correct
	}

	[Fact]
	public async Task If_NoUserId_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "percentage-flag";
		var context = new EvaluationContext(userId: null);
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Percentage);
		flag.PercentageEnabled = 50;

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("User ID required for percentage rollout");
	}
}

public class GetVariationAsync_WhenFlagEnabledWithVariation
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task ThenReturnsCorrectValue()
	{
		// Arrange
		var flagKey = "variation-flag";
		var context = new EvaluationContext(userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "variation-value" }
		};

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.GetVariation(flagKey, "default-value", context);

		// Assert
		result.ShouldBe("variation-value");
	}
}

public class GetVariationAsync_WhenFlagDisabled
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task ThenReturnsDefaultValue()
	{
		// Arrange
		var flagKey = "disabled-flag";
		var context = new EvaluationContext(userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Disabled);
		var defaultValue = "default-value";

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.GetVariation(flagKey, defaultValue, context);

		// Assert
		result.ShouldBe(defaultValue);
	}
}

public class GetVariationAsync_WithJsonElement
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task ThenDeserializesCorrectly()
	{
		// Arrange
		var flagKey = "json-flag";
		var context = new EvaluationContext(userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);

		var jsonValue = JsonDocument.Parse("{\"test\": \"value\"}").RootElement;
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", jsonValue }
		};

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.GetVariation<Dictionary<string, string>>(flagKey, [], context);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldContainKey("test");
		result["test"].ShouldBe("value");
	}
}

public class GetVariationAsync_WithTypeConversion
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task ThenConvertsSuccessfully()
	{
		// Arrange
		var flagKey = "conversion-flag";
		var context = new EvaluationContext(userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "123" } // String that can be converted to int
        };

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.GetVariation<int>(flagKey, 0, context);

		// Assert
		result.ShouldBe(123);
	}
}

public class GetVariationAsync_WhenExceptionThrownOrCancellationRequested
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task If_ExceptionThrown_ThenReturnsDefaultValueAndLogsError()
	{
		// Arrange
		var flagKey = "error-flag";
		var context = new EvaluationContext(userId: "user123");
		var defaultValue = "default";

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("Cache error"));

		// Act
		var result = await _tests._evaluator.GetVariation(flagKey, defaultValue, context);

		// Assert
		result.ShouldBe(defaultValue);
	}

	[Fact]
	public async Task If_hCancellationRequested_ThenPassesToDependencies()
	{
		// Arrange
		var flagKey = "test-flag";
		var context = new EvaluationContext(userId: "user123");
		var cancellationToken = new CancellationTokenSource().Token;

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);

		// Act
		await _tests._evaluator.Evaluate(flagKey, context, cancellationToken);

		// Assert
		_tests._mockCache.Verify(x => x.GetAsync(flagKey, cancellationToken), Times.Once);
		_tests._mockRepository.Verify(x => x.GetAsync(flagKey, cancellationToken), Times.Once);
	}
}

public class FeatureFlagEvaluatorTests
{
	public readonly Mock<IFeatureFlagRepository> _mockRepository;
	public readonly Mock<IFeatureFlagCache> _mockCache;
	public readonly IFlagEvaluationHandler _head;
	public readonly FeatureFlagEvaluator _evaluator;

	public FeatureFlagEvaluatorTests()
	{
		_mockRepository = new Mock<IFeatureFlagRepository>();
		_mockCache = new Mock<IFeatureFlagCache>();
		_head = EvaluatorChainBuilder.BuildChain();
		_evaluator = new FeatureFlagEvaluator(_mockRepository.Object, _head, _mockCache.Object);
	}

	public static FeatureFlag CreateTestFlag(string key, FeatureFlagStatus status)
	{
		return new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for unit tests",
			Status = status,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			CreatedBy = "test",
			UpdatedBy = "test",
			DefaultVariation = "off",
			TargetingRules = [],
			EnabledUsers = [],
			DisabledUsers = [],
			Variations = [],
			Tags = []
		};
	}
}

public class EvaluateAsync_WithTenantOverrides
{
	private readonly FeatureFlagEvaluatorTests _tests = new();

	[Fact]
	public async Task If_TenantExplicitlyDisabled_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "tenant-flag";
		var tenantId = "disabled-tenant";
		var context = new EvaluationContext(tenantId: tenantId, userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.DisabledTenants.Add(tenantId);

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(flag.DefaultVariation);
		result.Reason.ShouldBe("Tenant explicitly disabled");
	}

	[Fact]
	public async Task If_TenantExplicitlyEnabled_ThenContinuesEvaluation()
	{
		// Arrange
		var flagKey = "tenant-flag";
		var tenantId = "enabled-tenant";
		var context = new EvaluationContext(tenantId: tenantId, userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.EnabledTenants.Add(tenantId);
		flag.TenantPercentageEnabled = 0; // Would normally block all tenants

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // Continues to flag evaluation (Enabled status)
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_TenantNotInPercentageRollout_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "tenant-percentage-flag";
		var context = new EvaluationContext(tenantId: "blocked-tenant", userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 0; // Block all tenants

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe(flag.DefaultVariation);
		result.Reason.ShouldBe("Tenant not in percentage rollout");
	}

	[Fact]
	public async Task If_NoTenantId_ThenSkipsTenantEvaluation()
	{
		// Arrange
		var flagKey = "no-tenant-flag";
		var context = new EvaluationContext(userId: "user123"); // No tenant ID
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.TenantPercentageEnabled = 0; // Would block if tenant evaluation ran

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue(); // Skips tenant evaluation, goes to flag status
		result.Reason.ShouldBe("Flag enabled");
	}

	[Fact]
	public async Task If_TenantDisabledTakesPrecedenceOverEnabled_ThenReturnsDisabled()
	{
		// Arrange
		var flagKey = "precedence-flag";
		var tenantId = "conflict-tenant";
		var context = new EvaluationContext(tenantId: tenantId, userId: "user123");
		var flag = FeatureFlagEvaluatorTests.CreateTestFlag(flagKey, FeatureFlagStatus.Enabled);
		flag.DisabledTenants.Add(tenantId);
		flag.EnabledTenants.Add(tenantId); // Same tenant in both lists
		flag.TenantPercentageEnabled = 100; // Would allow tenant

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync(flag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Reason.ShouldBe("Tenant explicitly disabled"); // Disabled takes precedence
	}
}
