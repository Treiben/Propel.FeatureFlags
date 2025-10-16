using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.SqlServer.Helpers;

internal static class RepositoryHelpers
{
	internal static async Task GenerateAuditRecordAsync(FlagIdentifier identifier,
							SqlConnection connection,
							CancellationToken cancellationToken)
	{
		const string sql = @"-- Audit log entry
					INSERT INTO FeatureFlagsAudit (
						FlagKey, ApplicationName, ApplicationVersion, Action, Actor, Timestamp, Notes
					) VALUES (
						@key, @applicationName, @applicationVersion, @action, 'Application', @timestamp, 'Auto-registered by the application'
					)";

		try
		{
			using var command = new SqlCommand(sql, connection);
			command.AddIdentifierParameters(identifier);
			command.Parameters.AddWithValue("@timestamp", DateTimeOffset.UtcNow);
			command.Parameters.AddWithValue("@action", "flag-created");

			if (connection.State != System.Data.ConnectionState.Open)
				await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch
		{
			//no biggie - just log it
		}
	}

	internal static async Task GenerateMetadataRecordAsync(FlagIdentifier identifier, SqlConnection connection, CancellationToken cancellationToken)
	{
		const string sql = @"
            INSERT INTO FeatureFlagsMetadata (
                FlagKey, ApplicationName, ApplicationVersion, ExpirationDate, IsPermanent
            ) VALUES (
                @key, @applicationName, @applicationVersion, @expirationDate, @isPermanent
            )";
		try
		{
			using var command = new SqlCommand(sql, connection);
			command.AddIdentifierParameters(identifier);
			command.Parameters.AddWithValue("@expirationDate", DateTimeOffset.UtcNow.AddDays(30));
			command.Parameters.AddWithValue("@isPermanent", false);

			if (connection.State != System.Data.ConnectionState.Open)
				await connection.OpenAsync(cancellationToken);
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch
		{
			//no biggie - just log it
		}
	}

	internal static async Task<bool> CheckFlagExists(FlagIdentifier identifier, SqlConnection connection, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(identifier);
		var sql = $"SELECT COUNT(*) FROM FeatureFlags {whereClause}";

		using var command = new SqlCommand(sql, connection);
		command.AddWhereParameters(parameters);

		if (connection.State != System.Data.ConnectionState.Open)
			await connection.OpenAsync(cancellationToken);
		var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

		return count > 0;
	}

	private static void AddIdentifierParameters(this SqlCommand command, FlagIdentifier identifier)
	{
		command.Parameters.AddWithValue("@key", identifier.Key);
		command.Parameters.AddWithValue("@applicationName", identifier.ApplicationName);
		command.Parameters.AddWithValue("@applicationVersion", identifier.ApplicationVersion);
	}
}