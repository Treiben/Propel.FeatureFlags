using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql;

public class PostgresFeatureFlagRepository : IFeatureFlagRepository
{
	private readonly string _connectionString;
	private readonly ILogger<PostgresFeatureFlagRepository> _logger;

	public PostgresFeatureFlagRepository(string connectionString, ILogger<PostgresFeatureFlagRepository> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
		_logger.LogDebug("PostgreSQL Feature Flag Repository initialized with connection pooling");
	}

	public async Task<FlagEvaluationConfiguration?> GetAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flag with key: {Key}, Scope: {Scope}, Application: {Application}",
			identifier.Key, identifier.Scope, identifier.ApplicationName);

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
            INSERT INTO feature_flags (
                key, application_name, application_version, scope, name, description, evaluation_modes
            ) VALUES (
                @key, @application_name, @application_version, @scope, @name, @description, @evaluation_modes             
            )
			ON CONFLICT (key, application_name, application_version, scope) DO NOTHING;";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			bool flagAlreadyCreated = await FlagAuditHelpers.FlagAlreadyCreated(identifier, connection, cancellationToken);

			using var command = new NpgsqlCommand(sql, connection);
			command.Parameters.AddWithValue("key", identifier.Key);
			command.Parameters.AddWithValue("application_name", applicationName);
			command.Parameters.AddWithValue("application_version", applicationVersion);
			command.Parameters.AddWithValue("scope", (int)Scope.Application);
			command.Parameters.AddWithValue("name", name);
			command.Parameters.AddWithValue("description", description);
			var evaluationModesParam = command.Parameters.Add("evaluation_modes", NpgsqlDbType.Jsonb);
			evaluationModesParam.Value = JsonSerializer.Serialize(new List<int> { (int)mode }, JsonDefaults.JsonOptions);

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
