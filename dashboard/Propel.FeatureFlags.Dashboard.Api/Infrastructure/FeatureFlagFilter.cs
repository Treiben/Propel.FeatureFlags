using Propel.FeatureFlags.Domain;

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
