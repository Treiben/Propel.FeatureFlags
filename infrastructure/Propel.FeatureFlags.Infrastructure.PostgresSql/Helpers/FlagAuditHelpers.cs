using Knara.UtcStrict;
using Npgsql;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;

internal static class FlagAuditHelpers
{
	internal static async Task AddAuditTrail(FlagIdentifier flag,
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
			command.AddIdentifierParameters(flag);
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

	internal static async Task CreateInitialMetadataRecord(FlagIdentifier flag, string name, string description, NpgsqlConnection connection, CancellationToken cancellationToken)
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
			command.AddIdentifierParameters(flag);
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

	internal static async Task<bool> FlagAlreadyCreated(FlagIdentifier flag, NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flag);
		var sql = $"SELECT COUNT(*) FROM feature_flags {whereClause}";

		using var command = new NpgsqlCommand(sql, connection);
		command.AddWhereParameters(parameters);

		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync(cancellationToken);
		var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

		return count > 0;
	}

	internal static void AddIdentifierParameters(this NpgsqlCommand command, FlagIdentifier flag)
	{
		command.Parameters.AddWithValue("key", flag.Key);
		command.Parameters.AddWithValue("application_name", flag.ApplicationName ?? throw new ArgumentException("Application name must be provided!"));
		command.Parameters.AddWithValue("application_version", flag.ApplicationVersion ?? "1.0.0.0");
	}
}
