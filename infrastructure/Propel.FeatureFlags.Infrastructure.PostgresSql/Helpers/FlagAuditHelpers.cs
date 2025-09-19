using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;

public static class FlagAuditHelpers
{
	public static async Task AddAuditTrail(FlagIdentifier flagKey,
							NpgsqlConnection connection,
							CancellationToken cancellationToken)
	{
		const string sql = @"-- Audit log entry
					INSERT INTO feature_flags_audit (
						flag_key, application_name, application_version, action, actor, timestamp, reason
					) VALUES (
						@key, @application_name, @application_version, 'flag created', 'Application', @timestamp, 'Auto-registered by the application'
					);";

		try
		{
			using var command = new NpgsqlCommand(sql, connection);
			command.AddIdentifierParameters(flagKey);
			command.Parameters.AddWithValue("timestamp", DateTime.UtcNow);

			if (connection.State != System.Data.ConnectionState.Open)
				await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch
		{
			//no biggie - just log it
		}
	}

	public static async Task CreateInitialMetadataRecord(FlagIdentifier flagKey, string name, string description, NpgsqlConnection connection, CancellationToken cancellationToken)
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
			command.AddIdentifierParameters(flagKey);
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

	public static async Task<bool> FlagAlreadyCreated(FlagIdentifier flagKey, NpgsqlConnection connection, CancellationToken cancellationToken)
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

	public static void AddIdentifierParameters(this NpgsqlCommand command, FlagIdentifier flag)
	{
		command.Parameters.AddWithValue("key", flag.Key);
		command.Parameters.AddWithValue("application_name", (object?)flag.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("application_version", (object?)flag.ApplicationVersion ?? DBNull.Value);
	}
}
