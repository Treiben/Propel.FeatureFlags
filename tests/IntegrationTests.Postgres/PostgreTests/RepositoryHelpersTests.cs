using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.PostgreSql.Helpers;
using Shouldly;
using Xunit;

namespace FeatureFlags.IntegrationTests.Postgres.PostgreTests;

[Collection("Postgres")]
public class RepositoryHelpersTests : IClassFixture<PostgresTestsFixture>
{
    private readonly PostgresTestsFixture _fixture;
    private readonly string _connectionString;

    public RepositoryHelpersTests(PostgresTestsFixture fixture)
    {
        _fixture = fixture;
        _connectionString = fixture.GetConnectionString();
    }

    #region GenerateAuditRecordAsync Tests

    [Fact]
    public async Task GenerateAuditRecordAsync_ShouldInsertAuditRecord_WithCorrectAction()
    {
        // Arrange
        await _fixture.ClearAllData();
        await _fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
            "audit-test-flag",
            EvaluationMode.On,
            "Audit Test",
            "Testing audit",
            CancellationToken.None
        );

        var identifier = new FlagIdentifier(
            key: "audit-test-flag",
            scope: Scope.Application,
            applicationName: "TestApp",
            applicationVersion: "1.0.0.0"
        );

        // Act
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await RepositoryHelpers.GenerateAuditRecordAsync(identifier, connection, CancellationToken.None);

        // Assert
        using var verifyCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM feature_flags_audit WHERE flag_key = @key AND action = 'flag-created'",
            connection
        );
        verifyCommand.Parameters.AddWithValue("key", "audit-test-flag");
        var count = Convert.ToInt32(await verifyCommand.ExecuteScalarAsync());

        count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GenerateAuditRecordAsync_ShouldPopulateAllRequiredFields()
    {
        // Arrange
        await _fixture.ClearAllData();
        await _fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
            "complete-audit-flag",
            EvaluationMode.On,
            "Complete Audit",
            "Testing complete audit",
            CancellationToken.None
        );

        var identifier = new FlagIdentifier(
            key: "complete-audit-flag",
            scope: Scope.Application,
            applicationName: "TestApp",
            applicationVersion: "1.0.0.0"
        );

        // Act
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await RepositoryHelpers.GenerateAuditRecordAsync(identifier, connection, CancellationToken.None);

        // Assert
        using var verifyCommand = new NpgsqlCommand(@"
            SELECT flag_key, application_name, application_version, action, actor, notes 
            FROM feature_flags_audit 
            WHERE flag_key = @key AND application_name = @app_name
            ORDER BY timestamp DESC
            LIMIT 1",
            connection
        );
        verifyCommand.Parameters.AddWithValue("key", "complete-audit-flag");
        verifyCommand.Parameters.AddWithValue("app_name", "TestApp");

        using var reader = await verifyCommand.ExecuteReaderAsync();
        reader.Read().ShouldBeTrue();

        reader.GetString(0).ShouldBe("complete-audit-flag");
        reader.GetString(1).ShouldBe("TestApp");
        reader.GetString(2).ShouldBe("1.0.0.0");
        reader.GetString(3).ShouldBe("flag-created");
        reader.GetString(4).ShouldBe("Application");
        reader.GetString(5).ShouldBe("Auto-registered by the application");
    }

    #endregion

    #region GenerateMetadataRecordAsync Tests

    [Fact]
    public async Task GenerateMetadataRecordAsync_ShouldInsertMetadataRecord()
    {
        // Arrange
        await _fixture.ClearAllData();
        await _fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
            "metadata-test-flag",
            EvaluationMode.On,
            "Metadata Test",
            "Testing metadata",
            CancellationToken.None
        );

        var identifier = new FlagIdentifier(
            key: "metadata-test-flag",
            scope: Scope.Application,
            applicationName: "TestApp",
            applicationVersion: "1.0.0.0"
        );

        // Act
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await RepositoryHelpers.GenerateMetadataRecordAsync(identifier, connection, CancellationToken.None);

        // Assert
        using var verifyCommand = new NpgsqlCommand(
            "SELECT COUNT(*) FROM feature_flags_metadata WHERE flag_key = @key",
            connection
        );
        verifyCommand.Parameters.AddWithValue("key", "metadata-test-flag");
        var count = Convert.ToInt32(await verifyCommand.ExecuteScalarAsync());

        count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GenerateMetadataRecordAsync_ShouldSetExpirationDateAndPermanentFlag()
    {
        // Arrange
        await _fixture.ClearAllData();
        await _fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
            "expiration-test-flag",
            EvaluationMode.On,
            "Expiration Test",
            "Testing expiration",
            CancellationToken.None
        );

        var identifier = new FlagIdentifier(
            key: "expiration-test-flag",
            scope: Scope.Application,
            applicationName: "TestApp",
            applicationVersion: "1.0.0.0"
        );

        var beforeInsert = DateTimeOffset.UtcNow;

        // Act
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await RepositoryHelpers.GenerateMetadataRecordAsync(identifier, connection, CancellationToken.None);

        // Assert
        using var verifyCommand = new NpgsqlCommand(@"
            SELECT expiration_date, is_permanent 
            FROM feature_flags_metadata 
            WHERE flag_key = @key AND application_name = @app_name
            ORDER BY expiration_date DESC
            LIMIT 1",
            connection
        );
        verifyCommand.Parameters.AddWithValue("key", "expiration-test-flag");
        verifyCommand.Parameters.AddWithValue("app_name", "TestApp");

        using var reader = await verifyCommand.ExecuteReaderAsync();
        reader.Read().ShouldBeTrue();

        var expirationDate = reader.GetFieldValue<DateTimeOffset>(0);
        var isPermanent = reader.GetBoolean(1);

        expirationDate.ShouldBeGreaterThan(beforeInsert.AddDays(29));
        expirationDate.ShouldBeLessThan(beforeInsert.AddDays(31));
        isPermanent.ShouldBeFalse();
    }

    #endregion

    #region CheckFlagExists Tests

    [Fact]
    public async Task CheckFlagExists_ShouldReturnTrue_WhenFlagExists()
    {
        // Arrange
        await _fixture.ClearAllData();
        await _fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
            "exists-test-flag",
            EvaluationMode.On,
            "Exists Test",
            "Testing exists check",
            CancellationToken.None
        );

        var identifier = new FlagIdentifier(
            key: "exists-test-flag",
            scope: Scope.Application,
            applicationName: ApplicationInfo.Name,
            applicationVersion: ApplicationInfo.Version
		);

        // Act
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        var exists = await RepositoryHelpers.CheckFlagExists(identifier, connection, CancellationToken.None);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckFlagExists_ShouldReturnFalse_WhenFlagDoesNotExist()
    {
        // Arrange
        await _fixture.ClearAllData();
        var identifier = new FlagIdentifier(
            key: "non-existent-flag",
            scope: Scope.Application,
            applicationName: "TestApp",
            applicationVersion: "1.0.0.0"
        );

        // Act
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        var exists = await RepositoryHelpers.CheckFlagExists(identifier, connection, CancellationToken.None);

        // Assert
        exists.ShouldBeFalse();
    }

    #endregion
}