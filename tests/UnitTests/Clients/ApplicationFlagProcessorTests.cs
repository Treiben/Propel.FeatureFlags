using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;

namespace UnitTests.Clients;

public class ApplicationFlagProcessorTests
{
    private readonly Mock<IFeatureFlagRepository> _mockRepository;
    private readonly Mock<IEvaluatorsSet> _mockEvaluators;
    private readonly Mock<IFeatureFlagCache> _mockCache;
    private readonly Mock<IFeatureFlag> _mockFlag;
    private readonly ApplicationFlagProcessor _processor;

    public ApplicationFlagProcessorTests()
    {
        _mockRepository = new Mock<IFeatureFlagRepository>();
        _mockEvaluators = new Mock<IEvaluatorsSet>();
        _mockCache = new Mock<IFeatureFlagCache>();
        _mockFlag = new Mock<IFeatureFlag>();
        
        _processor = new ApplicationFlagProcessor(
            _mockRepository.Object,
            _mockEvaluators.Object,
            _mockCache.Object
        );
    }

    #region Evaluate Tests

    [Fact]
    public async Task Evaluate_ShouldReturnEnabledResult_WhenFlagExistsAndEvaluatesToTrue()
    {
        // Arrange
        var flagKey = "test-flag";
		var context = new EvaluationContext();
        var evaluationOptions = new EvaluationOptions(flagKey, new ModeSet([EvaluationMode.On]));
        var expectedResult = new EvaluationResult(isEnabled: true, reason: "Flag is enabled");

        _mockFlag.Setup(f => f.Key).Returns(flagKey);
        _mockCache.Setup(c => c.GetAsync(It.IsAny<FlagCacheKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationOptions?)null);
        _mockRepository.Setup(r => r.GetEvaluationOptionsAsync(It.IsAny<FlagIdentifier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluationOptions);
        _mockEvaluators.Setup(e => e.Evaluate(evaluationOptions, context))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _processor.Evaluate(_mockFlag.Object, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Reason.ShouldBe("Flag is enabled");
    }

    [Fact]
    public async Task Evaluate_ShouldAutoCreateFlagAndReturnOnMode_WhenFlagDoesNotExist()
    {
        // Arrange
        var flagKey = "new-flag";
		var identifier = new ApplicationFlagIdentifier(flagKey);
		var context = new EvaluationContext();

        _mockFlag.Setup(f => f.Key).Returns(flagKey);
        _mockFlag.Setup(f => f.OnOffMode).Returns(EvaluationMode.On);
        _mockFlag.Setup(f => f.Name).Returns("New Flag");
        _mockFlag.Setup(f => f.Description).Returns("A new flag");

        _mockCache.Setup(c => c.GetAsync(It.IsAny<FlagCacheKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationOptions?)null);
        _mockRepository.Setup(r => r.GetEvaluationOptionsAsync(identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationOptions?)null);
        _mockRepository.Setup(r => r.CreateApplicationFlagAsync(
			identifier,
            EvaluationMode.On,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _processor.Evaluate(_mockFlag.Object, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Reason.ShouldContain("application flag created");
    }

    [Fact]
    public async Task Evaluate_ShouldReturnDisabledResult_WhenExceptionOccurs()
    {
        // Arrange
        var flagKey = "error-flag";
		var identifier = new ApplicationFlagIdentifier(flagKey);
		var context = new EvaluationContext();

        _mockFlag.Setup(f => f.Key).Returns(flagKey);
        _mockCache.Setup(c => c.GetAsync(It.IsAny<FlagCacheKey>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _processor.Evaluate(_mockFlag.Object, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeFalse();
        result.Reason.ShouldContain("Error during evaluation");
    }

    #endregion

    #region GetVariation Tests

    [Fact]
    public async Task GetVariation_ShouldReturnVariationValue_WhenFlagExistsAndIsEnabled()
    {
        // Arrange
        var flagKey = "variation-flag";
		var context = new EvaluationContext();
        var defaultValue = "default";
        var expectedVariation = "premium";
        
        var variations = new Variations
        {
            Values = new Dictionary<string, object>
            {
                { "control", "basic" },
                { "treatment", "premium" }
            },
            DefaultVariation = "control"
        };

        var evaluationOptions = new EvaluationOptions(
            flagKey,
            new ModeSet([EvaluationMode.On]),
            variations: variations
        );

        var evaluationResult = new EvaluationResult(isEnabled: true, variation: "treatment");

        _mockFlag.Setup(f => f.Key).Returns(flagKey);
        _mockCache.Setup(c => c.GetAsync(It.IsAny<FlagCacheKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationOptions?)null);
        _mockRepository.Setup(r => r.GetEvaluationOptionsAsync(It.IsAny<FlagIdentifier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluationOptions);
        _mockEvaluators.Setup(e => e.Evaluate(evaluationOptions, context))
            .ReturnsAsync(evaluationResult);

        // Act
        var result = await _processor.GetVariation(_mockFlag.Object, defaultValue, context);

        // Assert
        result.ShouldBe(expectedVariation);
    }

    [Fact]
    public async Task GetVariation_ShouldReturnDefaultValue_WhenFlagDoesNotExist()
    {
        // Arrange
        var flagKey = "missing-flag";
		var identifier = new ApplicationFlagIdentifier(flagKey);
		var context = new EvaluationContext();
        var defaultValue = "default-variation";

        _mockFlag.Setup(f => f.Key).Returns(flagKey);
        _mockFlag.Setup(f => f.OnOffMode).Returns(EvaluationMode.Off);
        _mockCache.Setup(c => c.GetAsync(It.IsAny<FlagCacheKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationOptions?)null);
        _mockRepository.Setup(r => r.GetEvaluationOptionsAsync(identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationOptions?)null);

        // Act
        var result = await _processor.GetVariation(_mockFlag.Object, defaultValue, context);

        // Assert
        result.ShouldBe(defaultValue);
        _mockRepository.Verify(r => r.CreateApplicationFlagAsync(
            It.IsAny<FlagIdentifier>(),
            It.IsAny<EvaluationMode>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetVariation_ShouldReturnDefaultValue_WhenFlagIsDisabled()
    {
        // Arrange
        var flagKey = "disabled-flag";
		var identifier = new ApplicationFlagIdentifier(flagKey);
		var context = new EvaluationContext();
        var defaultValue = 100;

        var evaluationOptions = new EvaluationOptions(flagKey, new ModeSet([EvaluationMode.Off]));
        var evaluationResult = new EvaluationResult(isEnabled: false, reason: "Flag is disabled");

        _mockFlag.Setup(f => f.Key).Returns(flagKey);
        _mockCache.Setup(c => c.GetAsync(It.IsAny<FlagCacheKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(evaluationOptions);
        _mockEvaluators.Setup(e => e.Evaluate(evaluationOptions, context))
            .ReturnsAsync(evaluationResult);

        // Act
        var result = await _processor.GetVariation(_mockFlag.Object, defaultValue, context);

        // Assert
        result.ShouldBe(defaultValue);
    }

    #endregion

    #region Cache Integration Tests

    [Fact]
    public async Task Evaluate_ShouldUseCachedFlag_WhenAvailable()
    {
        // Arrange
        var flagKey = "cached-flag";
		var identifier = new ApplicationFlagIdentifier(flagKey);
		var context = new EvaluationContext();
        var cachedOptions = new EvaluationOptions(flagKey, new ModeSet([EvaluationMode.On]));
        var expectedResult = new EvaluationResult(isEnabled: true);

        _mockFlag.Setup(f => f.Key).Returns(flagKey);
        _mockCache.Setup(c => c.GetAsync(It.IsAny<FlagCacheKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedOptions);
        _mockEvaluators.Setup(e => e.Evaluate(cachedOptions, context))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _processor.Evaluate(_mockFlag.Object, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        _mockRepository.Verify(r => r.GetEvaluationOptionsAsync(
            It.IsAny<FlagIdentifier>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Evaluate_ShouldCacheAutoCreatedFlag()
    {
        // Arrange
        var flagKey = "auto-created-flag";
		var identifier = new ApplicationFlagIdentifier(flagKey);
		var context = new EvaluationContext();

        _mockFlag.Setup(f => f.Key).Returns(flagKey);
        _mockFlag.Setup(f => f.OnOffMode).Returns(EvaluationMode.Off);
        _mockFlag.Setup(f => f.Name).Returns("Auto Flag");
        _mockFlag.Setup(f => f.Description).Returns("Auto created");

        _mockCache.Setup(c => c.GetAsync(It.IsAny<FlagCacheKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationOptions?)null);
        _mockRepository.Setup(r => r.GetEvaluationOptionsAsync(identifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvaluationOptions?)null);

        // Act
        await _processor.Evaluate(_mockFlag.Object, context);

        // Assert
        _mockCache.Verify(c => c.SetAsync(
            It.IsAny<FlagCacheKey>(),
            It.Is<EvaluationOptions>(o => o.Key == flagKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}