using Microsoft.Extensions.Logging;
using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;

public class InDatabaseValidator
{
	private readonly string _connectionString;
	private readonly ILogger<InDatabaseValidator> _logger;

	public InDatabaseValidator(string connectionString, ILogger<InDatabaseValidator> logger)
	{
		_connectionString = connectionString;
		_logger = logger;
		_logger.LogDebug("PostgreSQL Feature Flag Repository initialized with connection pooling");
	}

	public async Task ValidateUniqueFlagAsync(FlagKey flagKey, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flagKey);
		var sql = $"SELECT COUNT(*) FROM feature_flags {whereClause}";

		using var connection = new NpgsqlConnection(_connectionString);
		using var command = new NpgsqlCommand(sql, connection);
		command.AddFilterParameters(parameters);

		await connection.OpenAsync(cancellationToken);
		var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

		if (count > 0)
		{
			var message = "Feature flag with key '{Key}' already exists in scope '{Scope}' for application '{ApplicationName}'";
			_logger.LogWarning(message, flagKey.Key, flagKey.Scope, flagKey.ApplicationName);
			throw new DuplicatedFeatureFlagException(flagKey.Key, flagKey.Scope, flagKey.ApplicationName);
		}
	}
}
