using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure.SqlServer.Extensions;
using Propel.FeatureFlags.Infrastructure.SqlServer.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.SqlServer;

public class ClientApplicationRepository : IFlagEvaluationRepository
{
	private readonly string _connectionString;
	private readonly ILogger<ClientApplicationRepository> _logger;

	public ClientApplicationRepository(string connectionString, ILogger<ClientApplicationRepository> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
		_logger.LogDebug("SQL Server Feature Flag Repository initialized with connection pooling");
	}

	public async Task<FlagEvaluationConfiguration?> GetAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flag with key: {Key}, Scope: {Scope}, Application: {Application}",
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
			using var connection = new SqlConnection(_connectionString);
			using var command = new SqlCommand(sql, connection);
			command.AddWhereParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			if (!await reader.ReadAsync(cancellationToken))
			{
				_logger.LogDebug("Feature flag with key {Key} not found within application {Application} scope", identifier.Key, identifier.ApplicationName);
				return null;
			}

			var flag = await reader.LoadAsync(identifier);
			_logger.LogDebug("Retrieved feature flag: {Key} with evaluation modes {Modes}",
				flag.Identifier.Key, string.Join(",", flag.ActiveEvaluationModes.Modes));
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving feature flag with key {Key} for {Application}", identifier.Key, identifier.ApplicationName);
			throw;
		}
	}

	public async Task CreateAsync(FlagIdentifier identifier, EvaluationMode mode, string name, string description, CancellationToken cancellationToken = default)
	{
		if (identifier.Scope == Scope.Global)
		{
			throw new InvalidOperationException("Only application-level flags are allowed to be created from client applications. Global flags are outside of application domain and must be created by management tools.");
		}

		_logger.LogDebug("Creating feature flag with key: {Key} for application: {Application}", identifier.Key, identifier.ApplicationName);

		var applicationName = identifier.ApplicationName;
		if (string.IsNullOrEmpty(identifier.ApplicationName))
		{
			applicationName = ApplicationInfo.Name;
		}

		var applicationVersion = identifier.ApplicationVersion;
		if (string.IsNullOrEmpty(identifier.ApplicationVersion))
		{
			applicationVersion = ApplicationInfo.Version ?? "1.0.0.0";
		}

		const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM FeatureFlags 
                          WHERE [Key] = @key AND ApplicationName = @applicationName 
                          AND ApplicationVersion = @applicationVersion AND Scope = @scope)
            BEGIN
                INSERT INTO FeatureFlags (
                    [Key], ApplicationName, ApplicationVersion, Scope, Name, Description, EvaluationModes
                ) VALUES (
                    @key, @applicationName, @applicationVersion, @scope, @name, @description, @evaluationModes             
                )
            END";

		try
		{
			using var connection = new SqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			bool flagAlreadyCreated = await FlagAuditHelpers.FlagAlreadyCreated(identifier, connection, cancellationToken);

			using var command = new SqlCommand(sql, connection);
			command.Parameters.AddWithValue("@key", identifier.Key);
			command.Parameters.AddWithValue("@applicationName", applicationName);
			command.Parameters.AddWithValue("@applicationVersion", applicationVersion);
			command.Parameters.AddWithValue("@scope", (int)Scope.Application);
			command.Parameters.AddWithValue("@name", name);
			command.Parameters.AddWithValue("@description", description);
			command.Parameters.AddWithValue("@evaluationModes", JsonSerializer.Serialize(new List<int> { (int)mode }, JsonDefaults.JsonOptions));

			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			if (rowsAffected == 0)
			{
				_logger.LogWarning("Feature flag with key '{Key}' already exists in scope '{Scope}' for application '{ApplicationName}'. Nothing to add there.",
					identifier.Key, identifier.Scope, identifier.ApplicationName);
				return;
			}

			await FlagAuditHelpers.CreateInitialMetadataRecord(identifier, name, description, connection, cancellationToken);
			await FlagAuditHelpers.AddAuditTrail(identifier, connection, cancellationToken);

			_logger.LogDebug("Successfully created feature flag: {Key}", identifier.Key);
		}
		catch (Exception ex) when (ex is not OperationCanceledException && ex is not InsertFlagException)
		{
			_logger.LogError(ex, "Error creating feature flag with key {Key} {Scope} {Application} {Version}",
				identifier.Key, identifier.Scope, identifier.ApplicationName, identifier.ApplicationVersion);

			throw new InsertFlagException("Error creating feature flag", ex,
				identifier.Key, identifier.Scope, identifier.ApplicationName, identifier.ApplicationVersion);
		}
	}
}