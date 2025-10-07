using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace FeatureFlags.IntegrationTests.Postgres.PostgreTests;

[Collection("Postgres")]
public class PostgresFeatureFlagRepositoryTests : IClassFixture<PostgresTestsFixture>
{
    private readonly PostgresTestsFixture _fixture;
    private readonly IFeatureFlagRepository _repository;

    public PostgresFeatureFlagRepositoryTests(PostgresTestsFixture fixture)
    {
        _fixture = fixture;
        _repository = _fixture.FeatureFlagRepository;
    }

    #region GetEvaluationOptionsAsync Tests

    [Fact]
    public async Task GetEvaluationOptionsAsync_ShouldReturnNull_WhenFlagDoesNotExist()
    {
        // Arrange
        await _fixture.ClearAllData();

        // Act
        var result = await _repository.GetEvaluationOptionsAsync("non-existent-flag");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetEvaluationOptionsAsync_ShouldReturnEvaluationOptions_WhenFlagExists()
    {
        // Arrange
        await _fixture.ClearAllData();
        await _repository.CreateApplicationFlagAsync(
            "test-flag",
            EvaluationMode.On,
            "Test Flag",
            "A test feature flag",
            CancellationToken.None
        );

        // Act
        var result = await _repository.GetEvaluationOptionsAsync("test-flag");

        // Assert
        result.ShouldNotBeNull();
        result.Key.ShouldBe("test-flag");
        result.ModeSet.ShouldNotBeNull();
        result.ModeSet.Modes.ShouldContain(EvaluationMode.On);
    }

    #endregion

    #region CreateApplicationFlagAsync Tests

    [Fact]
    public async Task CreateApplicationFlagAsync_ShouldCreateFlag_WithOnMode()
    {
        // Arrange
        await _fixture.ClearAllData();

        // Act
        await _repository.CreateApplicationFlagAsync(
            "on-mode-flag",
            EvaluationMode.On,
            "On Mode Flag",
            "Testing On activation mode",
            CancellationToken.None
        );

        // Assert
        var result = await _repository.GetEvaluationOptionsAsync("on-mode-flag");
        result.ShouldNotBeNull();
        result.Key.ShouldBe("on-mode-flag");
        result.ModeSet.Modes.ShouldContain(EvaluationMode.On);
    }

    [Fact]
    public async Task CreateApplicationFlagAsync_ShouldCreateFlag_WithOffMode()
    {
        // Arrange
        await _fixture.ClearAllData();

        // Act
        await _repository.CreateApplicationFlagAsync(
            "off-mode-flag",
            EvaluationMode.Off,
            "Off Mode Flag",
            "Testing Off activation mode",
            CancellationToken.None
        );

        // Assert
        var result = await _repository.GetEvaluationOptionsAsync("off-mode-flag");
        result.ShouldNotBeNull();
        result.Key.ShouldBe("off-mode-flag");
        result.ModeSet.Modes.ShouldContain(EvaluationMode.Off);
    }

    #endregion
}