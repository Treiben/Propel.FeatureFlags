using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;

public static class QueryBuilders
{
	public static (string whereClause, Dictionary<string, object> parameters) BuildWhereClause(FlagIdentifier flagKey, string prefix = "")
	{
		var parameters = new Dictionary<string, object>
		{
			["key"] = flagKey.Key,
			["scope"] = (int)flagKey.Scope
		};

		if (flagKey.Scope == Scope.Global)
		{
			return ($"WHERE {prefix}key = @key AND {prefix}scope = @scope", parameters);
		}

		// Application or Feature scope
		if (string.IsNullOrEmpty(flagKey.ApplicationName))
		{
			throw new ArgumentException("Application name required for application-scoped flags.", nameof(flagKey.ApplicationName));
		}

		parameters["application_name"] = flagKey.ApplicationName;

		if (!string.IsNullOrEmpty(flagKey.ApplicationVersion))
		{
			parameters["application_version"] = flagKey.ApplicationVersion;
			return ($"WHERE {prefix}key = @key AND {prefix}scope = @scope AND {prefix}application_name = @application_name AND {prefix}application_version = @application_version", parameters);
		}
		else
		{
			return ($"WHERE {prefix}key = @key AND {prefix}scope = @scope AND {prefix}application_name = @application_name AND {prefix}application_version IS NULL", parameters);
		}
	}

	public static void AddWhereParameters(this NpgsqlCommand command, Dictionary<string, object> parameters)
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
