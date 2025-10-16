using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.PostgreSql.Helpers;

internal static class QueryBuilders
{
	internal static (string whereClause, Dictionary<string, object> parameters) BuildWhereClause(FlagIdentifier identifier, string prefix = "")
	{
		var parameters = new Dictionary<string, object>
		{
			["key"] = identifier.Key,
			["scope"] = (int)identifier.Scope
		};

		if (identifier.Scope == Scope.Global)
		{
			return ($"WHERE {prefix}key = @key AND {prefix}scope = @scope", parameters);
		}

		// Application or Feature scope
		if (string.IsNullOrEmpty(identifier.ApplicationName))
		{
			throw new ArgumentException("Application name required for application-scoped flags.", nameof(identifier.ApplicationName));
		}

		parameters["application_name"] = identifier.ApplicationName;

		if (!string.IsNullOrEmpty(identifier.ApplicationVersion))
		{
			parameters["application_version"] = identifier.ApplicationVersion;
			return ($"WHERE {prefix}key = @key AND {prefix}scope = @scope AND {prefix}application_name = @application_name AND {prefix}application_version = @application_version", parameters);
		}
		else
		{
			return ($"WHERE {prefix}key = @key AND {prefix}scope = @scope AND {prefix}application_name = @application_name AND {prefix}application_version IS NULL", parameters);
		}
	}

	internal static void AddWhereParameters(this NpgsqlCommand command, Dictionary<string, object> parameters)
	{
		foreach (var (key, value) in parameters)
		{
			if (key.StartsWith("tag") && !key.StartsWith("tagKey"))
			{
				// JSONB parameter for tag values
				var parameter = command.Parameters.Add(key, NpgsqlDbType.Jsonb);
				parameter.Value = value;
			}
			else if (key.StartsWith("mode"))
			{
				// JSONB parameter for evaluation modes
				var parameter = command.Parameters.Add(key, NpgsqlDbType.Jsonb);
				parameter.Value = value;
			}
			else
			{
				command.Parameters.AddWithValue(key, value);
			}
		}
	}
}
