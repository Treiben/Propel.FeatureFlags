using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure.SqlServer.Helpers;

internal static class QueryBuilders
{
	internal static (string whereClause, Dictionary<string, object> parameters) BuildWhereClause(FlagIdentifier flagKey, string prefix = "")
	{
		var parameters = new Dictionary<string, object>
		{
			["Key"] = flagKey.Key,
			["Scope"] = (int)flagKey.Scope
		};

		if (flagKey.Scope == Scope.Global)
		{
			return ($"WHERE {prefix}[Key] = @key AND {prefix}Scope = @scope", parameters);
		}

		parameters["applicationName"] = flagKey.ApplicationName!;

		if (!string.IsNullOrWhiteSpace(flagKey.ApplicationVersion))
		{
			parameters["applicationVersion"] = flagKey.ApplicationVersion;
			return ($"WHERE {prefix}[Key] = @key AND {prefix}Scope = @scope AND {prefix}ApplicationName = @applicationName AND {prefix}ApplicationVersion = @applicationVersion", parameters);
		}
		else
		{
			return ($"WHERE {prefix}[Key] = @key AND {prefix}Scope = @scope AND {prefix}ApplicationName = @applicationName AND {prefix}ApplicationVersion IS NULL", parameters);
		}
	}

	internal static void AddWhereParameters(this SqlCommand command, Dictionary<string, object> parameters)
	{
		foreach (var (key, value) in parameters)
		{
			command.Parameters.AddWithValue($"@{key}", value);
		}
	}
}