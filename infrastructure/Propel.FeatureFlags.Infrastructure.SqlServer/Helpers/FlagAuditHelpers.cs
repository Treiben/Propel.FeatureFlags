using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.SqlServer.Helpers;

public static class FlagAuditHelpers
{
	public static async Task AddAuditTrail(FlagIdentifier flag,
							SqlConnection connection,
							CancellationToken cancellationToken)
	{
		const string sql = @"-- Audit log entry
					INSERT INTO feature_flags_audit (
						flag_key, application_name, application_version, action, actor, timestamp, reason
					) VALUES (
						@key, @application_name, @application_version, @action, 'Application', @timestamp, 'Auto-registered by the application'
					)";

		try
		{
			using var command = new SqlCommand(sql, connection);
			command.AddIdentifierParameters(flag);
			command.Parameters.AddWithValue("@timestamp", DateTimeOffset.UtcNow);
			command.Parameters.AddWithValue("@action", PersistenceActions.FlagCreated);

			if (connection.State != System.Data.ConnectionState.Open)
				await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch
		{
			//no biggie - just log it
		}
	}

	public static async Task CreateInitialMetadataRecord(FlagIdentifier flag, string name, string description, SqlConnection connection, CancellationToken cancellationToken)
	{
		const string sql = @"
            INSERT INTO feature_flags_metadata (
                flag_key, application_name, application_version, expiration_date, is_permanent
            ) VALUES (
                @key, @application_name, @application_version, @expiration_date, @is_permanent
            )";
		try
		{
			using var command = new SqlCommand(sql, connection);
			command.AddIdentifierParameters(flag);
			command.Parameters.AddWithValue("@expiration_date", DateTimeOffset.UtcNow.AddDays(30));
			command.Parameters.AddWithValue("@is_permanent", false);

			if (connection.State != System.Data.ConnectionState.Open)
				await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch
		{
			//no biggie - just log it
		}
	}

	public static async Task<bool> FlagAlreadyCreated(FlagIdentifier flag, SqlConnection connection, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flag);
		var sql = $"SELECT COUNT(*) FROM feature_flags {whereClause}";

		using var command = new SqlCommand(sql, connection);
		command.AddWhereParameters(parameters);

		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync(cancellationToken);
		var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

		return count > 0;
	}

	private static void AddIdentifierParameters(this SqlCommand command, FlagIdentifier flag)
	{
		command.Parameters.AddWithValue("@key", flag.Key);
		command.Parameters.AddWithValue("@application_name", flag.ApplicationName);
		command.Parameters.AddWithValue("@application_version", flag.ApplicationVersion);
	}
}