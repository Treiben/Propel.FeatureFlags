using Microsoft.Extensions.Logging;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Evaluator;

public class CreateDefaultFlagAsync_RepositorySuccess
{
	private readonly CreateDefaultFlagAsyncTests _tests = new();

	[Fact]
	public async Task ThenCallsRepositoryCreateAndReturnsCreatedFlag()
	{
		// Arrange
		var flagKey = "test-flag";
		var context = new EvaluationContext(userId: "user123");
		var expectedFlag = CreateExpectedDefaultFlag(flagKey);
		var createdFlag = CreateExpectedDefaultFlag(flagKey);
		createdFlag.CreatedAt = DateTime.UtcNow; // Simulate repository setting additional properties

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.CreateAsync(It.IsAny<FeatureFlag>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(createdFlag);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag not found, using default disabled flag");

		// Verify repository.CreateAsync was called with correct parameters
		_tests._mockRepository.Verify(x => x.CreateAsync(
			It.Is<FeatureFlag>(f => 
				f.Key == flagKey &&
				f.Name == flagKey &&
				f.Description == $"Auto-created flag for {flagKey}" &&
				f.Status == FeatureFlagStatus.Disabled &&
				f.CreatedBy == "System" &&
				f.UpdatedBy == "System" &&
				f.DefaultVariation == "off" &&
				f.Variations.ContainsKey("off") &&
				f.Variations.ContainsKey("on")),
			It.IsAny<CancellationToken>()), Times.Once);

		// Verify flag was not cached
		_tests._mockCache.Verify(x => x.SetAsync(flagKey, createdFlag, TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task ThenLogsInformationAboutFlagCreation()
	{
		// Arrange
		var flagKey = "log-test-flag";
		var context = new EvaluationContext(userId: "user123");
		var createdFlag = CreateExpectedDefaultFlag(flagKey);

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.CreateAsync(It.IsAny<FeatureFlag>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(createdFlag);

		// Act
		await _tests._evaluator.Evaluate(flagKey, context);
	}

	private static FeatureFlag CreateExpectedDefaultFlag(string flagKey)
	{
		var now = DateTime.UtcNow;
		return new FeatureFlag
		{
			Key = flagKey,
			Name = flagKey,
			Description = $"Auto-created flag for {flagKey}",
			Status = FeatureFlagStatus.Disabled,
			CreatedAt = now,
			UpdatedAt = now,
			CreatedBy = "System",
			UpdatedBy = "System",
			DefaultVariation = "off",
			Variations = new Dictionary<string, object>
			{
				{ "off", false },
				{ "on", true }
			}
		};
	}
}

public class CreateDefaultFlagAsync_RepositoryFailure
{
	private readonly CreateDefaultFlagAsyncTests _tests = new();

	[Fact]
	public async Task ThenLogsErrorButReturnsDefaultFlag()
	{
		// Arrange
		var flagKey = "error-flag";
		var context = new EvaluationContext(userId: "user123");
		var repositoryException = new InvalidOperationException("Database connection failed");

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.CreateAsync(It.IsAny<FeatureFlag>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(repositoryException);

		// Act
		var result = await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("off");
		result.Reason.ShouldBe("Flag not found, using default disabled flag");

		// Verify cache was still called with the in-memory default flag
		_tests._mockCache.Verify(x => x.SetAsync(
			flagKey, 
			It.Is<FeatureFlag>(f => f.Key == flagKey && f.Status == FeatureFlagStatus.Disabled), 
			TimeSpan.FromMinutes(5), 
			It.IsAny<CancellationToken>()), Times.Never);
	}
}

public class CreateDefaultFlagAsync_FlagStructureValidation
{
	private readonly CreateDefaultFlagAsyncTests _tests = new();

	[Theory]
	[InlineData("simple-flag")]
	[InlineData("flag-with-dashes")]
	[InlineData("flag_with_underscores")]
	[InlineData("flag.with.dots")]
	[InlineData("123-numeric-start")]
	public async Task ThenCreatesCorrectFlagStructure(string flagKey)
	{
		// Arrange
		var context = new EvaluationContext(userId: "user123");
		var createdFlag = new FeatureFlag
		{
			Key = flagKey,
			Name = flagKey,
			Description = $"Auto-created flag for {flagKey}",
			Status = FeatureFlagStatus.Disabled,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			CreatedBy = "System",
			UpdatedBy = "System",
			DefaultVariation = "off",
			Variations = new Dictionary<string, object> { { "off", false }, { "on", true } }
		};

		_tests._mockCache.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.GetAsync(flagKey, It.IsAny<CancellationToken>()))
			.ReturnsAsync((FeatureFlag?)null);
		_tests._mockRepository.Setup(x => x.CreateAsync(It.IsAny<FeatureFlag>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(createdFlag);

		// Act
		await _tests._evaluator.Evaluate(flagKey, context);

		// Assert
		_tests._mockRepository.Verify(x => x.CreateAsync(
			It.Is<FeatureFlag>(f =>
				f.Key == flagKey &&
				f.Name == flagKey &&
				f.Description == $"Auto-created flag for {flagKey}" &&
				f.Status == FeatureFlagStatus.Disabled &&
				f.CreatedBy == "System" &&
				f.UpdatedBy == "System" &&
				f.DefaultVariation == "off" &&
				f.Variations.Count == 2 &&
				f.Variations.ContainsKey("off") &&
				f.Variations.ContainsKey("on") &&
				f.Variations["off"].Equals(false) &&
				f.Variations["on"].Equals(true) &&
				f.TargetingRules.Count == 0 &&
				f.EnabledUsers.Count == 0 &&
				f.DisabledUsers.Count == 0 &&
				f.CreatedAt > DateTime.MinValue &&
				f.UpdatedAt > DateTime.MinValue),
			It.IsAny<CancellationToken>()), Times.Once);
	}
}

public class CreateDefaultFlagAsyncTests
{
	public readonly Mock<IFeatureFlagRepository> _mockRepository;
	public readonly Mock<IFeatureFlagCache> _mockCache;
	public readonly IFlagEvaluationHandler _evaluationHandler;
	public readonly FeatureFlagEvaluator _evaluator;

	public CreateDefaultFlagAsyncTests()
	{
		_mockRepository = new Mock<IFeatureFlagRepository>();
		_mockCache = new Mock<IFeatureFlagCache>();

		_evaluationHandler = EvaluatorChainBuilder.BuildChain();
		_evaluator = new FeatureFlagEvaluator(_mockRepository.Object, _evaluationHandler, _mockCache.Object);
	}
}