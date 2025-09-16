using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;

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

	public async Task<EvaluationCriteria?> GetAsync(FlagKey flagKey, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Getting feature flag with key: {Key}, Scope: {Scope}, Application: {Application}",
			flagKey, flagKey.Scope, flagKey.ApplicationName);

		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flagKey);
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
				_logger.LogDebug("Feature flag with key {Key} not found within scope {Scope}", flagKey, flagKey.Scope);
				return null;
			}

			var flag = await reader.LoadOnlyEvalationFields();
			_logger.LogDebug("Retrieved feature flag: {Key} with evaluation modes {Modes}",
				flag.FlagKey, string.Join(",", flag.ActiveEvaluationModes.Modes));
			return flag;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Error retrieving feature flag with key {Key}", flagKey);
			throw;
		}
	}

	public async Task CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		_logger.LogDebug("Creating feature flag with key: {Key} for application: {Application}",
			flag.Key, flag.Key.ApplicationName);

		const string sql = @"
            INSERT INTO feature_flags (
                key, name, description, evaluation_modes,
                expiration_date, is_permanent,
                application_name, application_version, scope
            ) VALUES (
                @key, @name, @description, @evaluation_modes,
                @expiration_date, @is_permanent,
                @application_name, @application_version, @scope
            );";

		try
		{
			using var connection = new NpgsqlConnection(_connectionString);
			await connection.OpenAsync(cancellationToken);

			bool flagAlreadyCreated = await FlagAuditHelpers.FlagAlreadyCreated(flag.Key, connection, cancellationToken);
			if (flagAlreadyCreated)
			{
				_logger.LogWarning("Feature flag with key '{Key}' already exists in scope '{Scope}' for application '{ApplicationName}'. Nothing to add there.",
					flag.Key.Key, flag.Key.Scope, flag.Key.ApplicationName);
				return;
			}
			using var command = new NpgsqlCommand(sql, connection);
			command.AddRequiredParameters(flag);

			var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
			if (rowsAffected == 0)
			{
				throw new FlagInsertException("Failed to add a feature flag: inserted 0 records",
					flag.Key.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);
			}

			await FlagAuditHelpers.AddAuditTrail(flag.Key, "flag created", flag.Created, connection, cancellationToken);

			_logger.LogDebug("Successfully created feature flag: {Key}", flag.Key);
		}
		catch (Exception ex) when (ex is not OperationCanceledException && ex is not FlagInsertException)
		{
			_logger.LogError(ex, "Error creating feature flag with key {Key} {Scope} {Application} {Version}",
				flag.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);

			throw new FlagInsertException("Error creating feature flag", ex,
				flag.Key.Key, flag.Key.Scope, flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		}
	}
}
