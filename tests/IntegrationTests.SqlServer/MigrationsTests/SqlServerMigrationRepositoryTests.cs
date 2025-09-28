namespace FeatureFlags.IntegrationTests.SqlServer.MigrationsTests;

public class SqlServerMigrationRepositoryTests(MigrationsTestsFixture fixture) : IClassFixture<MigrationsTestsFixture>
{

    [Fact]
    public async Task GetAppliedMigrationsAsync_WhenMigrationsExist_ShouldReturnOrderedList()
    {
		// Arrange
		await fixture.SetupMigrations();
		await fixture.ClearMigrations();

		await fixture.MigrationRepository.RecordMigrationAsync("V1_0_0", "First migration", default);
        await fixture.MigrationRepository.RecordMigrationAsync("V1_0_1", "Second migration", default);

        // Act
        var migrations = await fixture.MigrationRepository.GetAppliedMigrationsAsync();

        // Assert
        migrations.ShouldContain("V1_0_0");
        migrations.ShouldContain("V1_0_1");
        migrations.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RecordMigrationAsync_WhenCalled_ShouldInsertMigrationRecord()
    {
		// Arrange
		await fixture.SetupMigrations();
        await fixture.ClearMigrations();

		// Act
		await fixture.MigrationRepository.RecordMigrationAsync("V1_0_0", "Test migration", default);

        // Assert
        var migrations = await fixture.MigrationRepository.GetAppliedMigrationsAsync();
        migrations.ShouldContain("V1_0_0");
    }

    [Fact]
    public async Task RecordMigrationAsync_WhenDuplicateVersion_ShouldThrowException()
    {
		// Arrange
		await fixture.SetupMigrations();
		await fixture.ClearMigrations();

		await fixture.MigrationRepository.RecordMigrationAsync("V1_0_0", "Test migration", default);

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () => 
            await fixture.MigrationRepository.RecordMigrationAsync("V1_0_0", "Duplicate migration", default));
    }

    [Fact]
    public async Task RemoveMigrationAsync_WhenMigrationExists_ShouldRemoveRecord()
    {
		// Arrange
		await fixture.SetupMigrations();
		await fixture.ClearMigrations();

		await fixture.MigrationRepository.RecordMigrationAsync("V1_0_0", "Test migration", default);

        // Act
        await fixture.MigrationRepository.RemoveMigrationAsync("V1_0_0", default);

        // Assert
        var migrations = await fixture.MigrationRepository.GetAppliedMigrationsAsync();
        migrations.ShouldNotContain("V1_0_0");
    }

    [Fact]
    public async Task RemoveMigrationAsync_WhenMigrationDoesNotExist_ShouldNotThrow()
    {
		// Arrange
		await fixture.SetupMigrations();
		await fixture.ClearMigrations();

		// Act & Assert
		await Should.NotThrowAsync(async () => 
            await fixture.MigrationRepository.RemoveMigrationAsync("V1_0_0", default));
    }
}