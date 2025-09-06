namespace Propel.FeatureFlags.Evaluation;

public class EvaluationResult(bool isEnabled, string? variation = default, string? reason = default,
	Dictionary<string, object>? metadata = default)
{
	public bool IsEnabled { get; } = isEnabled;
	public string Variation { get; } = variation ?? "off";
	public string Reason { get; } = reason ?? "";
	public Dictionary<string, object> Metadata { get; } = metadata ?? [];

	// Convenience methods for adding evaluation context to metadata
	public EvaluationResult WithTenant(string? tenantId)
	{
		if (!string.IsNullOrEmpty(tenantId))
		{
			Metadata["tenantId"] = tenantId!;
		}
		return this;
	}

	public EvaluationResult WithUser(string? userId)
	{
		if (!string.IsNullOrEmpty(userId))
		{
			Metadata["userId"] = userId!;
		}
		return this;
	}
}
