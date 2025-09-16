using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;

public static class FlagAuditHelpers
{
	public static async Task AddAuditTrail(FlagKey flagKey,
							string action,
							AuditTrail? lastModified,
							NpgsqlConnection connection,
							CancellationToken cancellationToken)
	{
		const string sql = @"-- Audit log entry
					INSERT INTO feature_flag_audit (
						flag_key, application_name, application_version, action, actor, timestamp, reason
					) VALUES (
						@key, @application_name, @application_version, @action, @actor, @timestamp, @reason
					);";

		using var command = new NpgsqlCommand(sql, connection);
		command.AddAuditParameters(flagKey,
			action: action,
			actor: lastModified?.Actor ?? "anonymous",
			reason: lastModified?.Reason ?? "not specified");

		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync(cancellationToken);
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public static async Task<bool> FlagAlreadyCreated(FlagKey flagKey, NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flagKey);
		var sql = $"SELECT COUNT(*) FROM feature_flags {whereClause}";

		using var command = new NpgsqlCommand(sql, connection);
		command.AddFilterParameters(parameters);

		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync(cancellationToken);
		var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

		return count > 0;
	}
}
