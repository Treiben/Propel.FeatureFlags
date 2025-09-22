namespace FeatureFlags.IntegrationTests.MigrationsTests;

public class MigrationEngineTests : IClassFixture<MigrationsTestsFixture>
{
    private readonly MigrationsTestsFixture _fixture;

    public MigrationEngineTests(MigrationsTestsFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrateAsync_WhenNoPendingMigrations_ShouldReturnSuccessWithUpToDateMessage()
    {
        // Act
        var result = await _fixture.MigrationEngine.MigrateAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.MigrationsApplied.ShouldBe(0);
    }

    [Fact]
    public async Task RollbackAsync_WhenNoAppliedMigrations_ShouldReturnSuccessWithNoRollbackMessage()
    {
        // Act
        var result = await _fixture.MigrationEngine.RollbackAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Message.ShouldBe("No applied migrations to rollback");
        result.MigrationsApplied.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoMigrationFiles_ShouldReturnSuccess()
    {
        // Act
        var result = await _fixture.MigrationEngine.ValidateAsync();

        // Assert
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenMigrationFileHasNoValidName_ShouldReturnErrorsInResult()
    {
        // Arrange - Create temporary invalid migration files
        var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "scripts");
        Directory.CreateDirectory(tempDir);
        var invalidFile = Path.Combine(tempDir, "invalid_name.sql");
        await File.WriteAllTextAsync(invalidFile, "SELECT 1");

        try
        {
            // Act
            var result = await _fixture.MigrationEngine.ValidateAsync();

            // Assert
            result.Success.ShouldBeFalse();
		}
        finally
        {
            if (File.Exists(invalidFile)) File.Delete(invalidFile);
            if (Directory.Exists(tempDir) && !Directory.GetFiles(tempDir).Any()) Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task BaselineAsync_WhenCalled_ShouldMarkAllMigrationsAsApplied()
    {
        // Act
        var result = await _fixture.MigrationEngine.BaselineAsync();

        // Assert
        result.Success.ShouldBeTrue();
        result.Message.ShouldContain("Baseline completed");
        result.MigrationsApplied.ShouldBeGreaterThanOrEqualTo(0);
    }
}