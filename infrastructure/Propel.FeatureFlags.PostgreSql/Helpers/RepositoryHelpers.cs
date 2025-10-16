using Npgsql;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.PostgreSql.Helpers;

internal static class RepositoryHelpers
{
	internal static async Task GenerateAuditRecordAsync(FlagIdentifier identifier,
							NpgsqlConnection connection,
							CancellationToken cancellationToken)
	{
		const string sql = @"-- Audit log entry
					INSERT INTO feature_flags_audit (
						flag_key, application_name, application_version, action, actor, timestamp, notes
					) VALUES (
						@key, @application_name, @application_version, @action, 'Application', @timestamp, 'Auto-registered by the application'
					);";

		try
		{
			using var command = new NpgsqlCommand(sql, connection);
			command.AddPrimaryKeyParameters(identifier);
			command.Parameters.AddWithValue("timestamp", DateTimeOffset.UtcNow);
			command.Parameters.AddWithValue("action", "flag-created");

			if (connection.State != System.Data.ConnectionState.Open)
				await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch
		{
			//no biggie - just log it
		}
	}

	internal static async Task GenerateMetadataRecordAsync(FlagIdentifier identifier, NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		const string sql = @"
            INSERT INTO feature_flags_metadata (
                flag_key, application_name, application_version, expiration_date, is_permanent
            ) VALUES (
                @key, @application_name, @application_version, @expiration_date, @is_permanent
            );";
		try
		{
			using var command = new NpgsqlCommand(sql, connection);
			command.AddPrimaryKeyParameters(identifier);
			command.Parameters.AddWithValue("expiration_date", DateTimeOffset.UtcNow.AddDays(30));
			command.Parameters.AddWithValue("is_permanent", false);

			if (connection.State != System.Data.ConnectionState.Open)
				await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch
		{
			//no biggie - just log it
		}
	}

	internal static async Task<bool> CheckFlagExists(FlagIdentifier identifier, NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(identifier);
		var sql = $"SELECT COUNT(*) FROM feature_flags {whereClause}";

		using var command = new NpgsqlCommand(sql, connection);
		command.AddWhereParameters(parameters);

		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync(cancellationToken);
		var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

		return count > 0;
	}

	internal static void AddPrimaryKeyParameters(this NpgsqlCommand command, FlagIdentifier identifier)
	{
		command.Parameters.AddWithValue("key", identifier.Key);
		command.Parameters.AddWithValue("application_name", identifier.ApplicationName);
		command.Parameters.AddWithValue("application_version", identifier.ApplicationVersion);
	}
}
