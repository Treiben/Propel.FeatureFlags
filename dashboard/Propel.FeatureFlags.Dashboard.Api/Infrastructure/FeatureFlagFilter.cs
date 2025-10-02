using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public class FeatureFlagFilter
{
	public Dictionary<string, string>? Tags { get; set; }
	public EvaluationMode[]? EvaluationModes { get; set; }
	public int? ExpiringInDays { get; set; }
	public string ApplicationName { get; set; } = string.Empty;
	public string? ApplicationVersion { get; set; }
	public Scope? Scope { get; set; }
}

public static class PostgresFiltering
{
	public static string BuildFilterQuery(int page, int pageSize, FeatureFlagFilter filter)
	{
		var (whereClause, parameters) = BuildFilterConditions(filter);

		var sql = $@"
        SELECT ff.*,
				ffm.""flag_key"" as ""flag_key"",
				ffm.""is_permanent"", ffm.""expiration_date"", ffm.""tags"", 
				ffa.""action"", ffa.""actor"", ffa.""timestamp"", ffa.""notes""
        FROM ""feature_flags"" ff
        LEFT JOIN ""feature_flags_metadata"" ffm ON ff.""key"" = ffm.""flag_key"" 
				AND ff.""application_name"" = ffm.""application_name""
				AND ff.""application_version"" = ffm.""application_version""
        LEFT JOIN ""feature_flags_audit"" ffa ON ff.""key"" = ffa.""flag_key"" 
            AND ff.""application_name"" = ffa.""application_name""
			AND ff.""application_version"" = ffa.""application_version""
        {whereClause}
        ORDER BY ff.""name"", ff.""key""
        OFFSET {(page - 1) * pageSize} ROWS 
        FETCH NEXT {pageSize} ROWS ONLY";

		return sql;
	}

	public static string BuildCountQuery(FeatureFlagFilter filter)
	{
		var (whereClause, parameters) = BuildFilterConditions(filter);

		var countSql = $@"SELECT COUNT(*) 
		FROM ""feature_flags"" ff
        LEFT JOIN ""feature_flags_metadata"" ffm ON ff.""key"" = ffm.""flag_key"" 
				AND ff.""application_name"" = ffm.""application_name""
				AND ff.""application_version"" = ffm.""application_version""
		 {whereClause}";

		return countSql;
	}

	public static (string whereClause, Dictionary<string, object> parameters) BuildFilterConditions(FeatureFlagFilter? filter)
	{
		var conditions = new List<string>();
		var parameters = new Dictionary<string, object>();

		if (filter == null)
			return (string.Empty, parameters);

		// Application name filtering - use ff table (primary identifier)
		if (!string.IsNullOrEmpty(filter.ApplicationName))
		{
			var appNameParam = "appName";
			parameters[appNameParam] = filter.ApplicationName;
			conditions.Add($"ff.\"application_name\" = {{{appNameParam}}}");
		}

		// Flag scope filtering
		if (filter.Scope.HasValue)
		{
			var scopeParam = "scope";
			parameters[scopeParam] = (int)filter.Scope.Value;
			conditions.Add($"ff.\"scope\" = {{{scopeParam}}}");
		}

		// Expiration date filtering - use ffm table (metadata specific)
		if (filter.ExpiringInDays.HasValue && filter.ExpiringInDays.Value > 0)
		{
			var targetDate = DateTimeOffset.UtcNow.AddDays(filter.ExpiringInDays.Value).Date;
			var expiringInDaysParam = "expiringInDays";
			parameters[expiringInDaysParam] = targetDate;
			conditions.Add($"DATE(ffm.\"expiration_date\") = {{{expiringInDaysParam}}}");
		}

		// Evaluation modes filtering - use ff table (main flag data)
		if (filter.EvaluationModes != null && filter.EvaluationModes.Length > 0)
		{
			var modeConditions = new List<string>();
			for (int i = 0; i < filter.EvaluationModes.Length; i++)
			{
				var modeParam = $"mode{i}";
				var modeJson = JsonSerializer.Serialize(new[] { (int)filter.EvaluationModes[i] }, JsonDefaults.JsonOptions);
				parameters[modeParam] = modeJson;
				modeConditions.Add($"ff.\"evaluation_modes\"::jsonb @> {{{modeParam}}}::jsonb");
			}
			conditions.Add($"({string.Join(" OR ", modeConditions)})");
		}

		// Tags filtering - use ffm table (metadata specific)
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
					parameters[keyParam] = tag.Key;
					tagConditions.Add($"ffm.\"tags\"::jsonb ? {{{keyParam}}}");
				}
				else
				{
					// Search by exact key-value match
					var tagParam = $"tag{tagIndex}";
					var tagJson = JsonSerializer.Serialize(new Dictionary<string, string> { [tag.Key] = tag.Value }, JsonDefaults.JsonOptions);
					parameters[tagParam] = tagJson;
					tagConditions.Add($"ffm.\"tags\"::jsonb @> {{{tagParam}}}::jsonb");
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

public static class SqlServerFiltering
{
	public static string BuildFilterQuery(int page, int pageSize, FeatureFlagFilter filter)
	{
		var (whereClause, parameters) = BuildFilterConditions(filter);
		var sql = $@"
		SELECT ff.*, 
			ffm.""FlagKey"" as ""FlagKey"",
			ffm.""IsPermanent"", ffm.""ExpirationDate"", ffm.""Tags"", 
			ffa.""Action"", ffa.""Actor"", ffa.""Timestamp"", ffa.""Notes""
		FROM ""FeatureFlags"" ff
		LEFT JOIN ""FeatureFlagsMetadata"" ffm ON ff.""Key"" = ffm.""FlagKey"" 
			AND ff.""ApplicationName"" = ffm.""ApplicationName""
		LEFT JOIN ""FeatureFlagsAudit"" ffa ON ff.""Key"" = ffa.""FlagKey"" 
			AND ff.""ApplicationName"" = ffa.""ApplicationName""
		{whereClause}
		ORDER BY ff.""Name"", ff.""Key""
		OFFSET {(page - 1) * pageSize} ROWS 
		FETCH NEXT {pageSize} ROWS ONLY";
		return sql;
	}
	public static string BuildCountQuery(FeatureFlagFilter filter)
	{
		var (whereClause, parameters) = BuildFilterConditions(filter);
		var sql = $@"
		SELECT COUNT(*) 
		FROM ""FeatureFlags"" ff
		LEFT JOIN ""FeatureFlagsMetadata"" ffm ON ff.""Key"" = ffm.""FlagKey"" 
			AND ff.""ApplicationName"" = ffm.""ApplicationName""
		{whereClause}";
		return sql;
	}
	public static (string whereClause, Dictionary<string, object> parameters) BuildFilterConditions(FeatureFlagFilter? filter)
	{
		var conditions = new List<string>();
		var parameters = new Dictionary<string, object>();

		if (filter == null)
			return (string.Empty, parameters);

		// Application name filtering
		if (!string.IsNullOrEmpty(filter.ApplicationName))
		{
			var appNameParam = "appName";
			parameters[appNameParam] = filter.ApplicationName;
			conditions.Add($"ff.[ApplicationName] = @{appNameParam}");
		}

		// Flag scope filtering
		if (filter.Scope.HasValue)
		{
			var scopeParam = "scope";
			parameters[scopeParam] = (int)filter.Scope.Value;
			conditions.Add($"ff.[Scope] = @{scopeParam}");
		}

		// Expiration date filtering
		if (filter.ExpiringInDays.HasValue && filter.ExpiringInDays.Value > 0)
		{
			var targetDate = DateTimeOffset.UtcNow.AddDays(filter.ExpiringInDays.Value).Date;
			var expiringInDaysParam = "expiringInDays";
			parameters[expiringInDaysParam] = targetDate;
			conditions.Add($"CAST(ffm.[ExpirationDate] AS DATE) = @{expiringInDaysParam}");
		}

		// Evaluation modes filtering - SQL Server JSON functions
		if (filter.EvaluationModes != null && filter.EvaluationModes.Length > 0)
		{
			var modeConditions = new List<string>();
			for (int i = 0; i < filter.EvaluationModes.Length; i++)
			{
				var modeParam = $"mode{i}";
				var modeValue = (int)filter.EvaluationModes[i];
				parameters[modeParam] = modeValue.ToString();

				// Use SQL Server JSON_VALUE and STRING_SPLIT for array containment
				modeConditions.Add($@"
                EXISTS (
                    SELECT 1 FROM STRING_SPLIT(
                        REPLACE(REPLACE(ff.[EvaluationModes], '[', ''), ']', ''), ','
                    ) 
                    WHERE LTRIM(RTRIM(value)) = @{modeParam}
                )");
			}
			conditions.Add($"({string.Join(" OR ", modeConditions)})");
		}

		// Tags filtering - SQL Server JSON functions
		if (filter.Tags != null && filter.Tags.Count > 0)
		{
			var tagConditions = new List<string>();
			var tagIndex = 0;

			foreach (var tag in filter.Tags)
			{
				if (string.IsNullOrEmpty(tag.Value))
				{
					// Search by tag key only using JSON_QUERY
					var keyParam = $"tagKey{tagIndex}";
					parameters[keyParam] = tag.Key;
					tagConditions.Add($"JSON_VALUE(ffm.[Tags], '$.{tag.Key}') IS NOT NULL");
				}
				else
				{
					// Search by exact key-value match
					var tagKeyParam = $"tagKey{tagIndex}";
					var tagValueParam = $"tagValue{tagIndex}";
					parameters[tagKeyParam] = tag.Key;
					parameters[tagValueParam] = tag.Value;

					tagConditions.Add($"JSON_VALUE(ffm.[Tags], '$.{tag.Key}') = @{tagValueParam}");
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

