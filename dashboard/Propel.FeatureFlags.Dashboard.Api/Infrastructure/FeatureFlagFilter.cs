using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public record FeatureFlagFilter(Dictionary<string, string>? Tags = null,
	EvaluationMode[]? EvaluationModes = null,
	string? ApplicationName = null,
	Scope? Scope = null);

public static class PostgresFiltering
{
	public static string BuildFilterQuery(int page, int pageSize, FeatureFlagFilter filter)
	{
		var sql = $@"
			WITH latest_audit AS ( 
				SELECT DISTINCT ON (flag_key, application_name, application_version) 
					a.flag_key, 
					a.application_name, 
					a.application_version, 
					a.action, 
					a.actor, 
					a.timestamp, 
					a.notes 
				FROM 
					feature_flags_audit a 
				ORDER BY 
					flag_key, application_name, application_version, timestamp DESC 
			) 
			SELECT 
				ff.*, 
				ffm.flag_key as flag_key, 
				ffm.is_permanent as is_permanent, 
				ffm.expiration_date as expiration_date, 
				ffm.tags as tags, 
				ffa.action as action, 
				ffa.actor as actor, 
				ffa.timestamp as timestamp, 
				ffa.notes as notes 
			FROM 
				feature_flags ff 
			LEFT JOIN 
				feature_flags_metadata ffm ON ff.key = ffm.flag_key 
					AND ff.application_name = ffm.application_name 
					AND ff.application_version = ffm.application_version 
			LEFT JOIN 
				latest_audit ffa ON ff.key = ffa.flag_key 
					AND ff.application_name = ffa.application_name 
					AND ff.application_version = ffa.application_version";

		if (filter != null)
		{
			var (whereClause, parameters) = BuildFilterConditions(filter);
			sql += $" {whereClause} ";
		}

		return sql += $@"
			ORDER BY ff.name, ff.key 
			OFFSET {(page - 1) * pageSize} ROWS 
			FETCH NEXT {pageSize} ROWS ONLY";
	}

	public static string BuildCountQuery(FeatureFlagFilter filter)
	{
		var sql = $@"SELECT COUNT(*) 
		FROM feature_flags ff 
			LEFT JOIN feature_flags_metadata ffm ON ff.key = ffm.flag_key 
					AND ff.application_name = ffm.application_name 
					AND ff.application_version = ffm.application_version";

		if (filter != null)
		{
			var (whereClause, parameters) = BuildFilterConditions(filter);
			return sql += $@"{whereClause}";
		}

		return sql;
	}

	public static (string whereClause, Dictionary<string, object> parameters) BuildFilterConditions(FeatureFlagFilter filter)
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
			conditions.Add($"ff.application_name = {{{appNameParam}}}");
		}

		// Flag scope filtering
		if (filter.Scope.HasValue)
		{
			var scopeParam = "scope";
			parameters[scopeParam] = (int)filter.Scope.Value;
			conditions.Add($"ff.scope = {{{scopeParam}}}");
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
				modeConditions.Add($"ff.evaluation_modes::jsonb @> {{{modeParam}}}::jsonb");
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
					tagConditions.Add($"ffm.tags::jsonb ? {{{keyParam}}}");
				}
				else
				{
					// Search by exact key-value match
					var tagParam = $"tag{tagIndex}";
					var tagJson = JsonSerializer.Serialize(new Dictionary<string, string> { [tag.Key] = tag.Value }, JsonDefaults.JsonOptions);
					parameters[tagParam] = tagJson;
					tagConditions.Add($"ffm.tags::jsonb @> {{{tagParam}}}::jsonb");
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
		var sql = $@"
			SELECT 
				ff.*, 
				ffm.FlagKey as FlagKey, 
				ffm.IsPermanent as IsPermanent, 
				ffm.ExpirationDate as ExpirationDate, 
				ffm.Tags as Tags, 
				ffa.[Action] as [Action], 
				ffa.Actor as Actor, 
				ffa.[Timestamp] as [Timestamp], 
				ffa.Notes as Notes 
			FROM 
				FeatureFlags ff 
			LEFT JOIN 
				FeatureFlagsMetadata ffm ON ff.[Key] = ffm.FlagKey 
					AND ff.ApplicationName = ffm.ApplicationName 
					AND ff.ApplicationVersion = ffm.ApplicationVersion 
			LEFT JOIN 
				FeatureFlagsAudit ffa ON ff.[Key] = ffa.FlagKey 
					AND ff.ApplicationName = ffa.ApplicationName 
					AND ff.ApplicationVersion = ffa.ApplicationVersion 
					AND ffa.[Timestamp] = (
						SELECT MAX(ffa2.[Timestamp]) 
						FROM FeatureFlagsAudit ffa2 
						WHERE ffa2.FlagKey = ff.[Key] 
							AND ffa2.ApplicationName = ff.ApplicationName 
							AND ffa2.ApplicationVersion = ff.ApplicationVersion
					)";

		if (filter != null)
		{
			var (whereClause, parameters) = BuildFilterConditions(filter);
			sql += $" {whereClause} ";
		}

		return sql += $@"
			ORDER BY ff.[Name], ff.[Key] 
			OFFSET {(page - 1) * pageSize} ROWS 
			FETCH NEXT {pageSize} ROWS ONLY";
	}

	public static string BuildCountQuery(FeatureFlagFilter filter)
	{
		var sql = $@"SELECT COUNT(*) 
		FROM FeatureFlags ff 
			LEFT JOIN FeatureFlagsMetadata ffm ON ff.[Key] = ffm.FlagKey 
					AND ff.ApplicationName = ffm.ApplicationName 
					AND ff.ApplicationVersion = ffm.ApplicationVersion";

		if (filter != null)
		{
			var (whereClause, parameters) = BuildFilterConditions(filter);
			return sql += $@"{whereClause}";
		}

		return sql;
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
			conditions.Add($"ff.ApplicationName = @{appNameParam}");
		}

		// Flag scope filtering
		if (filter.Scope.HasValue)
		{
			var scopeParam = "scope";
			parameters[scopeParam] = (int)filter.Scope.Value;
			conditions.Add($"ff.Scope = @{scopeParam}");
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
				modeConditions.Add($@"EXISTS (
					SELECT 1 FROM OPENJSON(ff.EvaluationModes) 
					WHERE value IN (SELECT value FROM OPENJSON(@{modeParam}))
				)");
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
				var keyParam = $"tagKey{tagIndex}";
				if (string.IsNullOrEmpty(tag.Value))
				{
					// Search by tag key only
					parameters[keyParam] = tag.Key;
					tagConditions.Add($@"EXISTS (
						SELECT 1 FROM OPENJSON(ffm.Tags) 
						WHERE [key] = @{keyParam}
					)");
				}
				else
				{
					// Search by exact key-value match
					var tagParam = $"tag{tagIndex}";
					var tagJson = JsonSerializer.Serialize(new Dictionary<string, string> { [tag.Key] = tag.Value }, JsonDefaults.JsonOptions);
					parameters[tagParam] = tagJson;
					tagConditions.Add($@"EXISTS (
						SELECT 1 
						FROM OPENJSON(ffm.Tags) 
						WHERE [key] = @{keyParam} AND [value] = @{tagParam}
					)");
					parameters[keyParam] = tag.Key;
					parameters[tagParam] = tag.Value;
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

