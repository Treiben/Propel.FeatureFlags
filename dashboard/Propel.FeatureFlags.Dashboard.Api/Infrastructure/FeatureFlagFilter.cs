using Microsoft.EntityFrameworkCore;
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

public static class Filtering
{
	public static IQueryable<Entities.FeatureFlag> ApplyFilters(IQueryable<Entities.FeatureFlag> query, FeatureFlagFilter filter,
		string provider = "")
	{
		// Filter by application scope
		if (!string.IsNullOrEmpty(filter.ApplicationName))
		{
			query = query.Where(f => f.ApplicationName == filter.ApplicationName);
		}

		if (!string.IsNullOrEmpty(filter.ApplicationVersion))
		{
			query = query.Where(f => f.ApplicationVersion == filter.ApplicationVersion);
		}

		if (filter.Scope.HasValue)
		{
			query = query.Where(f => f.Scope == (int)filter.Scope.Value);
		}

		if (filter.EvaluationModes != null && filter.EvaluationModes.Length > 0)
		{
			if (provider != null && provider.Contains("SqlServer"))
			{
				// SQL Server specific JSON filtering
				var modes = string.Join(",", filter.EvaluationModes.Select(m => (int)m));
				foreach (var mode in filter.EvaluationModes)
				{
					var modeValue = ((int)mode).ToString();
					query = query.Where(p => p.EvaluationModes.Contains(modeValue));
				}
			}
			else
			{
				// Default JSON filtering for other providers (e.g., SQLite, PostgreSQL)
				var modes = JsonSerializer.Serialize(filter.EvaluationModes.Select(m => (int)m), JsonDefaults.JsonOptions) ?? "[]";
				query = query.Where(p => EF.Functions.JsonContains(p.EvaluationModes, modes));
			}
		}

		return query;
	}

	public static bool FilterByEvaluationModes(string evaluationModesJson, EvaluationMode[] filterModes)
	{
		try
		{
			var modes = JsonSerializer.Deserialize<int[]>(evaluationModesJson) ?? [];
			return filterModes.Any(filterMode => modes.Contains((int)filterMode));
		}
		catch
		{
			return false;
		}
	}
}
