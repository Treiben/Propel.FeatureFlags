using Microsoft.Data.Sqlite;
using Propel.FeatureFlags.Domain;
using System.Collections.Generic;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Helpers
{
	internal static class QueryBuilders
	{
		internal static (string whereClause, Dictionary<string, object> parameters) BuildWhereClause(FlagIdentifier flagKey, string prefix = "")
		{
			var parameters = new Dictionary<string, object>
			{
				["key"] = flagKey.Key,
				["scope"] = (int)flagKey.Scope
			};

			if (flagKey.Scope == Scope.Global)
			{
				// SQLite doesn't use square brackets for identifiers, they're optional
				return ($"WHERE {prefix}Key = @key AND {prefix}Scope = @scope", parameters);
			}

			parameters["applicationName"] = flagKey.ApplicationName ?? "global";

			if (!string.IsNullOrWhiteSpace(flagKey.ApplicationVersion))
			{
				parameters["applicationVersion"] = flagKey.ApplicationVersion;
				return ($"WHERE {prefix}Key = @key AND {prefix}Scope = @scope AND {prefix}ApplicationName = @applicationName AND {prefix}ApplicationVersion = @applicationVersion", parameters);
			}
			else
			{
				return ($"WHERE {prefix}Key = @key AND {prefix}Scope = @scope AND {prefix}ApplicationName = @applicationName AND {prefix}ApplicationVersion IS NULL", parameters);
			}
		}

		internal static void AddWhereParameters(this SqliteCommand command, Dictionary<string, object> parameters)
		{
			foreach (var p in parameters)
			{
				command.Parameters.AddWithValue($"@{p.Key}", p.Value);
			}
		}
	}
}