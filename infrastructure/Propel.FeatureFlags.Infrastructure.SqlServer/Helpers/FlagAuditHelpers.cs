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
					INSERT INTO FeatureFlagsAudit (
						FlagKey, ApplicationName, ApplicationVersion, Action, Actor, Timestamp, Notes
					) VALUES (
						@key, @applicationName, @applicationVersion, @action, 'Application', @timestamp, 'Auto-registered by the application'
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
            INSERT INTO FeatureFlagsMetadata (
                FlagKey, ApplicationName, ApplicationVersion, ExpirationDate, IsPermanent
            ) VALUES (
                @key, @applicationName, @applicationVersion, @expirationDate, @isPermanent
            )";
		try
		{
			using var command = new SqlCommand(sql, connection);
			command.AddIdentifierParameters(flag);
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

	public static async Task<bool> FlagAlreadyCreated(FlagIdentifier flag, SqlConnection connection, CancellationToken cancellationToken)
	{
		var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flag);
		var sql = $"SELECT COUNT(*) FROM FeatureFlags {whereClause}";

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
		command.Parameters.AddWithValue("@applicationName", flag.ApplicationName);
		command.Parameters.AddWithValue("@applicationVersion", flag.ApplicationVersion);
	}
}