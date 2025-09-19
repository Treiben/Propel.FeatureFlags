using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql;

public class FlagEvaluationRepository : IFlagEvaluationRepository
{
	private readonly string _connectionString;
	private readonly ILogger<FlagEvaluationRepository> _logger;

	public FlagEvaluationRepository(string connectionString, ILogger<FlagEvaluationRepository> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
		_logger.LogDebug("PostgreSQL Feature Flag Repository initialized with connection pooling");
	}

	public async Task<FlagEvaluationConfiguration?> GetAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flag with key: {Key}, Scope: {Scope}, Application: {Application}",
			identifier, identifier.Scope, identifier.ApplicationName);

		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(identifier);
		var sql = $@"SELECT key,
					evaluation_modes,
					scheduled_enable_date, 
					scheduled_disable_date,
					window_start_time,
					window_end_time, 
					time_zone, 
					window_days,
					user_percentage_enabled, 
					targeting_rules, 
					enabled_users, 
					disabled_users,
					enabled_tenants, 
					disabled_tenants, 
					tenant_percentage_enabled,
					variations, 
					default_variation
					FROM feature_flags {whereClause}";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			using var command = new NpgsqlCommand(sql, connection);
			command.AddFilterParameters(parameters);

			await connection.OpenAsync(cancellationToken);
			using var reader = await command.ExecuteReaderAsync(cancellationToken);

			if (!await reader.ReadAsync(cancellationToken))
			{
				_logger.LogDebug("Feature flag with key {Key} not found within scope {Scope}", identifier, identifier.Scope);
				return null;
			}

			var flag = await reader.LoadAsync(identifier);
			_logger.LogDebug("Retrieved feature flag: {Key} with evaluation modes {Modes}",
				flag.Identifier, string.Join(",", flag.ActiveEvaluationModes.Modes));
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving feature flag with key {Key}", identifier);
			throw;
		}
	}

	public async Task CreateAsync(FlagIdentifier identifier, EvaluationMode mode, string name, string description, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Creating feature flag with key: {Key} for application: {Application}", identifier.Key, identifier.ApplicationName);

		const string sql = @"
            INSERT INTO feature_flags (
                key, name, description, scope, application_name, application_version, evaluation_modes
            ) VALUES (
                @key, @name, @description, @scope, @application_name, @application_version, @evaluation_modes             
            );";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			bool flagAlreadyCreated = await FlagAuditHelpers.FlagAlreadyCreated(identifier, connection, cancellationToken);
			if (flagAlreadyCreated)
			{
				_logger.LogWarning("Feature flag with key '{Key}' already exists in scope '{Scope}' for application '{ApplicationName}'. Nothing to add there.",
					identifier.Key, identifier.Scope, identifier.ApplicationName);
				return;
			}
			using var command = new NpgsqlCommand(sql, connection);
			command.AddIdentifierParameters(identifier);
			command.Parameters.AddWithValue("scope", (int)identifier.Scope);
			command.Parameters.AddWithValue("name", name);
			command.Parameters.AddWithValue("description", description);
			var evaluationModesParam = command.Parameters.Add("evaluation_modes", NpgsqlDbType.Jsonb);
			evaluationModesParam.Value = JsonSerializer.Serialize(new List<int> { (int)mode }, JsonDefaults.JsonOptions);

			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			if (rowsAffected == 0)
			{
				throw new InsertFlagException("Failed to add a feature flag: inserted 0 records",
					identifier.Key, identifier.Scope, identifier.ApplicationName, identifier.ApplicationVersion);
			}

			await FlagAuditHelpers.CreateInitialMetadataRecord(identifier, name, description, connection, cancellationToken);
			await FlagAuditHelpers.AddAuditTrail(identifier, connection, cancellationToken);

			_logger.LogDebug("Successfully created feature flag: {Key}", identifier);
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
