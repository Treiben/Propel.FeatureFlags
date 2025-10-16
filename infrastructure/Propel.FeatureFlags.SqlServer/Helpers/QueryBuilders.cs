using Microsoft.Data.SqlClient;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.SqlServer.Helpers;

internal static class QueryBuilders
{
	internal static (string whereClause, Dictionary<string, object> parameters) BuildWhereClause(FlagIdentifier identifier, string prefix = "")
	{
		var parameters = new Dictionary<string, object>
		{
			["Key"] = identifier.Key,
			["Scope"] = (int)identifier.Scope
		};

		if (identifier.Scope == Scope.Global)
		{
			return ($"WHERE {prefix}[Key] = @key AND {prefix}Scope = @scope", parameters);
		}

		parameters["applicationName"] = identifier.ApplicationName!;

		if (!string.IsNullOrWhiteSpace(identifier.ApplicationVersion))
		{
			parameters["applicationVersion"] = identifier.ApplicationVersion;
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