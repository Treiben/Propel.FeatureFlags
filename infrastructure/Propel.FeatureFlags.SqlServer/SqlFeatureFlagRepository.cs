using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;
using System.Text.Json;
using Propel.FeatureFlags.Utilities;
using Propel.FeatureFlags.SqlServer.Helpers;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.SqlServer;

internal sealed class SqlFeatureFlagRepository(string connectionString, ILogger<SqlFeatureFlagRepository> logger) : IFeatureFlagRepository
{
	public async Task<EvaluationOptions?> GetEvaluationOptionsAsync(string key, CancellationToken cancellationToken = default)
	{
		var identifier = new FlagIdentifier(key, Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		logger.LogDebug("Getting feature flag with key: {Key}, Scope: {Scope}, Application: {Application}",
			identifier.Key, identifier.Scope, identifier.ApplicationName);

		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(identifier);
		var sql = $@"SELECT [Key],
					EvaluationModes,
					ScheduledEnableDate, 
					ScheduledDisableDate,
					WindowStartTime,
					WindowEndTime, 
					TimeZone, 
					WindowDays,
					UserPercentageEnabled, 
					TargetingRules, 
					EnabledUsers, 
					DisabledUsers,
					TenantPercentageEnabled,
					EnabledTenants, 
					DisabledTenants, 
					Variations, 
					DefaultVariation
					FROM FeatureFlags {whereClause}";

		try
		{
			using var connection = new SqlConnection(connectionString);
			using var command = new SqlCommand(sql, connection);
			command.AddWhereParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			if (!await reader.ReadAsync(cancellationToken))
			{
				logger.LogDebug("Feature flag with key {Key} not found within application {Application} scope", identifier.Key, identifier.ApplicationName);
				return null;
			}

			var flag = await reader.LoadOptionsAsync(identifier);
			logger.LogDebug("Retrieved feature flag: {Key} with evaluation modes {Modes}",
				flag.Key, string.Join(",", flag.ModeSet.Modes));
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogError(ex, "Error retrieving feature flag with key {Key} for {Application}", identifier.Key, identifier.ApplicationName);
			throw;
		}
	}

	public async Task CreateApplicationFlagAsync(string key, EvaluationMode activeMode, string name, string description, CancellationToken cancellationToken = default)
	{
		var identifier = new FlagIdentifier(key, Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);

		logger.LogDebug("Creating feature flag with key: {Key} for application: {Application}", identifier.Key, identifier.ApplicationName);

		const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM FeatureFlags 
                          WHERE [Key] = @key AND ApplicationName = @applicationName AND ApplicationVersion = @applicationVersion)
            BEGIN
                INSERT INTO FeatureFlags (
                    [Key], ApplicationName, ApplicationVersion, Scope, Name, Description, EvaluationModes
                ) VALUES (
                    @key, @applicationName, @applicationVersion, @scope, @name, @description, @evaluationModes             
                )
            END";

		try
		{
			using var connection = new SqlConnection(connectionString);
			await connection.OpenAsync(cancellationToken);

			bool flagAlreadyCreated = await RepositoryHelpers.CheckFlagExists(identifier, connection, cancellationToken);

			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@key", identifier.Key);
			command.Parameters.AddWithValue("@applicationName", identifier.ApplicationName);
			command.Parameters.AddWithValue("@applicationVersion", identifier.ApplicationVersion);
			command.Parameters.AddWithValue("@scope", (int)Scope.Application);
			command.Parameters.AddWithValue("@name", name);
			command.Parameters.AddWithValue("@description", description);
			command.Parameters.AddWithValue("@evaluationModes", JsonSerializer.Serialize(new List<int> { (int)activeMode }, JsonDefaults.JsonOptions));

			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			if (rowsAffected == 0)
			{
				logger.LogWarning("Feature flag with key '{Key}' already exists in scope '{Scope}' for application '{ApplicationName}'. Nothing to add there.",
					identifier.Key, identifier.Scope, identifier.ApplicationName);
				return;
			}

			await RepositoryHelpers.GenerateMetadataRecordAsync(identifier, connection, cancellationToken);
			await RepositoryHelpers.GenerateAuditRecordAsync(identifier, connection, cancellationToken);

			logger.LogDebug("Successfully created feature flag: {Key}", identifier.Key);
		}
		catch (Exception ex) when (ex is not OperationCanceledException && ex is not ApplicationFlagException)
		{
			logger.LogError(ex, "Error creating feature flag with key {Key} {Scope} {Application} {Version}",
				identifier.Key, identifier.Scope, identifier.ApplicationName, identifier.ApplicationVersion);

			throw new ApplicationFlagException("Error creating feature flag", ex,
				identifier.Key, identifier.Scope, identifier.ApplicationName, identifier.ApplicationVersion);
		}
	}
}