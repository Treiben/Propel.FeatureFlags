using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Helpers;

public static class QueryBuilders
{
	public static (string whereClause, Dictionary<string, object> parameters) BuildWhereClause(FlagKey flagKey, string prefix = "")
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

	public static (string whereClause, Dictionary<string, object> parameters) BuildFilterConditions(FeatureFlagFilter? filter)
	{
		var conditions = new List<string>();
		var parameters = new Dictionary<string, object>();

		if (filter == null)
			return (string.Empty, parameters);

		// Scope filtering
		if (filter.Scope.HasValue)
		{
			conditions.Add("scope = @scope");
			parameters["scope"] = (int)filter.Scope.Value;
		}

		// Application filtering
		if (!string.IsNullOrEmpty(filter.ApplicationName))
		{
			conditions.Add("application_name = @application_name");
			parameters["application_name"] = filter.ApplicationName;
		}

		// Evaluation modes filtering
		if (filter.EvaluationModes != null && filter.EvaluationModes.Length > 0)
		{
			var modeConditions = new List<string>();
			for (int i = 0; i < filter.EvaluationModes.Length; i++)
			{
				var modeParam = $"mode{i}";
				modeConditions.Add($"evaluation_modes @> @{modeParam}");
				parameters[modeParam] = JsonSerializer.Serialize(new[] { (int)filter.EvaluationModes[i] }, JsonDefaults.JsonOptions);
			}
			conditions.Add($"({string.Join(" OR ", modeConditions)})");
		}

		// Expiration filtering
		if (filter.ExpiringInDays.HasValue && filter.ExpiringInDays.Value > 0)
		{
			var expiryDate = DateTime.UtcNow.AddDays(filter.ExpiringInDays.Value);
			conditions.Add("expiration_date <= @expiryDate AND is_permanent = false");
			parameters["expiryDate"] = expiryDate;
		}

		// Tags filtering
		if (filter.Tags != null && filter.Tags.Count > 0)
		{
			var tagConditions = new List<string>();
			var tagIndex = 0;

			foreach (var tag in filter.Tags)
			{
				if (string.IsNullOrEmpty(tag.Value))
				{
					// Search by tag key only
					var keyParam = $"tagKey{tagIndex}";
					tagConditions.Add($"tags ? @{keyParam}");
					parameters[keyParam] = tag.Key;
				}
				else
				{
					// Search by exact key-value match
					var tagParam = $"tag{tagIndex}";
					tagConditions.Add($"tags @> @{tagParam}");
					parameters[tagParam] = JsonSerializer.Serialize(new Dictionary<string, string> { [tag.Key] = tag.Value }, JsonDefaults.JsonOptions);
				}
				tagIndex++;
			}

			if (tagConditions.Count > 0)
			{
				conditions.Add($"({string.Join(" OR ", tagConditions)})");
			}
		}

		var whereClause = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : string.Empty;
		return (whereClause, parameters);
	}
}
