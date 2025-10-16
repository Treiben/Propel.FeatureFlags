using FeatureFlags.IntegrationTests.SqlServer.SqlServerTests;
using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.SqlServer.Helpers;
using Shouldly;

namespace FeatureFlags.IntegrationTests.Postgres.PostgreTests;

[Collection("Postgres")]
public class RepositoryHelpersTests(SqlServerTestsFixture fixture) : IClassFixture<SqlServerTestsFixture>
{
	private readonly string _connectionString = fixture.GetConnectionString();

	#region GenerateAuditRecordAsync Tests

	[Fact]
    public async Task GenerateAuditRecordAsync_ShouldInsertAuditRecord_WithCorrectAction()
    {
        // Arrange
        await fixture.ClearAllData();
		var identifier = new ApplicationFlagIdentifier("audit-test-flag");
		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
			identifier,
            EvaluationMode.On,
            "Audit Test",
            "Testing audit",
            CancellationToken.None
        );

        // Act
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await RepositoryHelpers.GenerateAuditRecordAsync(identifier, connection, CancellationToken.None);

        // Assert
        using var verifyCommand = new SqlCommand(
            "SELECT COUNT(*) FROM FeatureFlagsAudit WHERE FlagKey = @key AND Action = 'flag-created'",
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
        await fixture.ClearAllData();
		var identifier = new ApplicationFlagIdentifier("complete-audit-flag");
		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
			identifier,
            EvaluationMode.On,
            "Complete Audit",
            "Testing complete audit",
            CancellationToken.None
        );

        // Act
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await RepositoryHelpers.GenerateAuditRecordAsync(identifier, connection, CancellationToken.None);

        // Assert
        using var verifyCommand = new SqlCommand(@"
            SELECT TOP 1 FlagKey, ApplicationName, ApplicationVersion, Action, Actor, Notes
            FROM FeatureFlagsAudit 
            WHERE FlagKey = @key AND ApplicationName = @app_name
            ORDER BY Timestamp DESC",
            connection
        );
        verifyCommand.Parameters.AddWithValue("key", identifier.Key);
        verifyCommand.Parameters.AddWithValue("app_name", identifier.ApplicationName);

        using var reader = await verifyCommand.ExecuteReaderAsync();
        reader.Read().ShouldBeTrue();

        reader.GetString(0).ShouldBe(identifier.Key);
        reader.GetString(1).ShouldBe(identifier.ApplicationName);
        reader.GetString(2).ShouldBe(identifier.ApplicationVersion);
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
        await fixture.ClearAllData();
		var identifier = new ApplicationFlagIdentifier("metadata-test-flag");
		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
			identifier,
            EvaluationMode.On,
            "Metadata Test",
            "Testing metadata",
            CancellationToken.None
        );

        // Act
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await RepositoryHelpers.GenerateMetadataRecordAsync(identifier, connection, CancellationToken.None);

        // Assert
        using var verifyCommand = new SqlCommand(
            "SELECT COUNT(*) FROM FeatureFlagsMetadata WHERE FlagKey = @key",
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
        await fixture.ClearAllData();
		var identifier = new ApplicationFlagIdentifier("expiration-test-flag");
		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
            identifier,
            EvaluationMode.On,
            "Expiration Test",
            "Testing expiration",
            CancellationToken.None
        );

        var beforeInsert = DateTimeOffset.UtcNow;

        // Act
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await RepositoryHelpers.GenerateMetadataRecordAsync(identifier, connection, CancellationToken.None);

        // Assert
        using var verifyCommand = new SqlCommand(@"
            SELECT TOP 1 ExpirationDate, IsPermanent 
            FROM FeatureFlagsMetadata 
            WHERE FlagKey = @key AND ApplicationName = @app_name
            ORDER BY ExpirationDate DESC",
            connection
        );
        verifyCommand.Parameters.AddWithValue("key", identifier.Key);
        verifyCommand.Parameters.AddWithValue("app_name", identifier.ApplicationName);

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
        await fixture.ClearAllData();
		var identifier = new ApplicationFlagIdentifier("exists-test-flag");
		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
			identifier,
            EvaluationMode.On,
            "Exists Test",
            "Testing exists check",
            CancellationToken.None
        );

        // Act
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var exists = await RepositoryHelpers.CheckFlagExists(identifier, connection, CancellationToken.None);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckFlagExists_ShouldReturnFalse_WhenFlagDoesNotExist()
    {
        // Arrange
        await fixture.ClearAllData();
		var identifier = new ApplicationFlagIdentifier("non-existent-flag");

        // Act
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var exists = await RepositoryHelpers.CheckFlagExists(identifier, connection, CancellationToken.None);

        // Assert
        exists.ShouldBeFalse();
    }

    #endregion
}