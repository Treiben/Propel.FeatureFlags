namespace Propel.FeatureFlags.Evaluation;

public class EvaluationResult
{
	public EvaluationResult(bool isEnabled, string? variation = default, string? reason = default, 
		Dictionary<string, object>? metadata = default)
	{
		IsEnabled = isEnabled;
		Variation = variation ?? "off";
		Reason = reason ?? "";
		Metadata = metadata ?? [];
	}

	public bool IsEnabled { get; }
	public string Variation { get; }
	public string Reason { get; }
	public Dictionary<string, object> Metadata { get; }

	// Convenience methods for adding evaluation context to metadata
	public EvaluationResult WithTenant(string? tenantId)
	{
		if (!string.IsNullOrEmpty(tenantId))
		{
			Metadata["tenantId"] = tenantId;
		}
		return this;
	}

	public EvaluationResult WithUser(string? userId)
	{
		if (!string.IsNullOrEmpty(userId))
		{
			Metadata["userId"] = userId;
		}
		return this;
	}
}
