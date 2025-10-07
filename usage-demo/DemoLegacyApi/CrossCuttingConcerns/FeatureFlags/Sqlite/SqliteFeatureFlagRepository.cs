using DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Helpers;
using Microsoft.Data.Sqlite;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Utilities;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite
{
	internal sealed class SqliteFeatureFlagRepository : IFeatureFlagRepository
	{
		private readonly SqliteConnection _inMemoryConnection;

		public SqliteFeatureFlagRepository(SqliteConnection inMemoryConnection)
		{
			_inMemoryConnection = inMemoryConnection;
		}

		public async Task<EvaluationOptions?> GetEvaluationOptionsAsync(string key, CancellationToken cancellationToken = default)
		{
			var identifier = new FlagIdentifier(key, Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
			var (whereClause, parameters) = QueryBuilders.BuildWhereClause(identifier);
			
			// SQLite doesn't use square brackets for identifiers
			var sql = $@"SELECT Key,
					EvaluationModes,
					ScheduledEnableDate, 
					ScheduledDisableDate,
					WindowStartTime,
					WindowEndTime, 
					TimeZone, 
					WindowDays,
					UserPercentageEnabled, 
					TargetingRules, 
					EnabledUsers, 
					DisabledUsers,
					TenantPercentageEnabled,
					EnabledTenants, 
					DisabledTenants, 
					Variations, 
					DefaultVariation
					FROM FeatureFlags {whereClause}";

			try
			{
				using (var command = new SqliteCommand(sql, _inMemoryConnection))
				{
					command.AddWhereParameters(parameters);

					if (_inMemoryConnection.State != System.Data.ConnectionState.Open)
						await _inMemoryConnection.OpenAsync(cancellationToken);

					using (var reader = await command.ExecuteReaderAsync(cancellationToken))
					{
						if (!await reader.ReadAsync(cancellationToken))
						{
							return null;
						}

						var flag = await reader.LoadOptionsAsync(identifier);
						return flag;
					}
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				throw;
			}
		}

		public async Task CreateApplicationFlagAsync(string key, EvaluationMode activeMode, string name, string description, CancellationToken cancellationToken = default)
		{
			var identifier = new FlagIdentifier(key, Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);

			const string sql = @"
				INSERT OR IGNORE INTO FeatureFlags (
					Key, ApplicationName, ApplicationVersion, Scope, Name, Description, EvaluationModes
				) VALUES (
					@key, @applicationName, @applicationVersion, @scope, @name, @description, @evaluationModes
				)";

			try
			{
				if (_inMemoryConnection.State != System.Data.ConnectionState.Open)
					await _inMemoryConnection.OpenAsync(cancellationToken);

				bool flagAlreadyCreated = await FlagAuditHelpers.FlagAlreadyCreated(identifier, _inMemoryConnection, cancellationToken);

				using (var command = new SqliteCommand(sql, _inMemoryConnection))
				{
					command.Parameters.AddWithValue("@key", identifier.Key);
					command.Parameters.AddWithValue("@applicationName", identifier.ApplicationName);
					command.Parameters.AddWithValue("@applicationVersion", identifier.ApplicationVersion);
					command.Parameters.AddWithValue("@scope", (int)Scope.Application);
					command.Parameters.AddWithValue("@name", name);
					command.Parameters.AddWithValue("@description", description);
					command.Parameters.AddWithValue("@evaluationModes",
						JsonSerializer.Serialize(new List<int> { (int)activeMode }, JsonDefaults.JsonOptions));

					var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
					if (rowsAffected == 0)
					{
						return;
					}

					await FlagAuditHelpers.CreateInitialMetadataRecord(identifier, _inMemoryConnection, cancellationToken);
					await FlagAuditHelpers.AddAuditTrail(identifier, _inMemoryConnection, cancellationToken);
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException && ex is not ApplicationFlagException)
			{
				throw new ApplicationFlagException("Error creating feature flag", ex,
					identifier.Key, identifier.Scope, identifier.ApplicationName, identifier.ApplicationVersion);
			}
		}
	}
}