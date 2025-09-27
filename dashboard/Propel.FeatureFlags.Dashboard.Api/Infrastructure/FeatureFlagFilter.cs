using Propel.FeatureFlags.Domain;
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
	public static IQueryable<Entities.FeatureFlag> ApplyFilters(IQueryable<Entities.FeatureFlag> query, FeatureFlagFilter filter)
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

		// Filter by evaluation modes
		if (filter.EvaluationModes?.Length > 0)
		{
			query = query.Where(f => FilterByEvaluationModes(f.EvaluationModes, filter.EvaluationModes));
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
