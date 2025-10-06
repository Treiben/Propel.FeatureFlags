using Microsoft.Data.Sqlite;
using Propel.FeatureFlags.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Helpers
{
	internal static class FlagAuditHelpers
	{
		internal static async Task AddAuditTrail(
			FlagIdentifier flag,
			SqliteConnection connection,
			CancellationToken cancellationToken)
		{
			// SQLite uses different syntax for GUIDs and timestamps
			const string sql = @"
				INSERT INTO FeatureFlagsAudit (
					Id, FlagKey, ApplicationName, ApplicationVersion, Action, Actor, Timestamp, Notes
				) VALUES (
					@id, @key, @applicationName, @applicationVersion, @action, 'Application', @timestamp, 'Auto-registered by the application'
				)";

			try
			{
				using var command = new SqliteCommand(sql, connection);
				command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString()); // SQLite stores GUID as TEXT
				command.AddIdentifierParameters(flag);
				command.Parameters.AddWithValue("@timestamp", DateTimeOffset.UtcNow.ToString("O")); // ISO 8601 format
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

		internal static async Task CreateInitialMetadataRecord(
			FlagIdentifier flag,
			SqliteConnection connection,
			CancellationToken cancellationToken)
		{
			// SQLite requires explicit Id value
			const string sql = @"
				INSERT INTO FeatureFlagsMetadata (
					Id, FlagKey, ApplicationName, ApplicationVersion, ExpirationDate, IsPermanent
				) VALUES (
					@id, @key, @applicationName, @applicationVersion, @expirationDate, @isPermanent
				)";
			
			try
			{
				using var command = new SqliteCommand(sql, connection);
				command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString()); // SQLite stores GUID as TEXT
				command.AddIdentifierParameters(flag);
				command.Parameters.AddWithValue("@expirationDate", DateTimeOffset.UtcNow.AddDays(30).ToString("O")); // ISO 8601 format
				command.Parameters.AddWithValue("@isPermanent", 0); // SQLite uses 0/1 for boolean

				if (connection.State != System.Data.ConnectionState.Open)
					await connection.OpenAsync(cancellationToken);
				await command.ExecuteNonQueryAsync(cancellationToken);
			}
			catch
			{
				//no biggie - just log it
			}
		}

		internal static async Task<bool> FlagAlreadyCreated(
			FlagIdentifier flag,
			SqliteConnection connection,
			CancellationToken cancellationToken)
		{
			var (whereClause, parameters) = QueryBuilders.BuildWhereClause(flag);
			var sql = $"SELECT COUNT(*) FROM FeatureFlags {whereClause}";

			using var command = new SqliteCommand(sql, connection);
			command.AddWhereParameters(parameters);

			if (connection.State != System.Data.ConnectionState.Open)
				await connection.OpenAsync(cancellationToken);
			var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));

			return count > 0;
		}

		private static void AddIdentifierParameters(this SqliteCommand command, FlagIdentifier flag)
		{
			command.Parameters.AddWithValue("@key", flag.Key);
			command.Parameters.AddWithValue("@applicationName", flag.ApplicationName ?? "global");
			command.Parameters.AddWithValue("@applicationVersion", flag.ApplicationVersion ?? "0.0.0.0");
		}
	}
}