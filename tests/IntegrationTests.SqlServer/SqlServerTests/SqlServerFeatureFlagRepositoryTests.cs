using FeatureFlags.IntegrationTests.SqlServer.SqlServerTests;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace FeatureFlags.IntegrationTests.Postgres.PostgreTests;

[Collection("Postgres")]
public class SqlServerFeatureFlagRepositoryTests : IClassFixture<SqlServerTestsFixture>
{
    private readonly SqlServerTestsFixture _fixture;
    private readonly IFeatureFlagRepository _repository;

    public SqlServerFeatureFlagRepositoryTests(SqlServerTestsFixture fixture)
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
		var identifier = new ApplicationFlagIdentifier("non-existent-flag");
		// Act
		var result = await _repository.GetEvaluationOptionsAsync(identifier);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetEvaluationOptionsAsync_ShouldReturnEvaluationOptions_WhenFlagExists()
    {
        // Arrange
        await _fixture.ClearAllData();
		var identifier = new ApplicationFlagIdentifier("test-flag");
		await _repository.CreateApplicationFlagAsync(
			identifier,
            EvaluationMode.On,
            "Test Flag",
            "A test feature flag",
            CancellationToken.None
        );

        // Act
        var result = await _repository.GetEvaluationOptionsAsync(identifier);

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
		var identifier = new ApplicationFlagIdentifier("on-mode-flag");
		// Act
		await _repository.CreateApplicationFlagAsync(
			identifier,
            EvaluationMode.On,
            "On Mode Flag",
            "Testing On activation mode",
            CancellationToken.None
        );

        // Assert
        var result = await _repository.GetEvaluationOptionsAsync(identifier);
        result.ShouldNotBeNull();
        result.Key.ShouldBe(identifier.Key);
        result.ModeSet.Modes.ShouldContain(EvaluationMode.On);
    }

    [Fact]
    public async Task CreateApplicationFlagAsync_ShouldCreateFlag_WithOffMode()
    {
        // Arrange
        await _fixture.ClearAllData();
		var identifier = new ApplicationFlagIdentifier("off-mode-flag");
		// Act
		await _repository.CreateApplicationFlagAsync(
			identifier,
            EvaluationMode.Off,
            "Off Mode Flag",
            "Testing Off activation mode",
            CancellationToken.None
        );

        // Assert
        var result = await _repository.GetEvaluationOptionsAsync(identifier);
        result.ShouldNotBeNull();
        result.Key.ShouldBe(identifier.Key);
        result.ModeSet.Modes.ShouldContain(EvaluationMode.Off);
    }

    #endregion
}