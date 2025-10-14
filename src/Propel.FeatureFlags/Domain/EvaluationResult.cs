namespace Propel.FeatureFlags.Domain;

/// <summary>
/// Represents the result of an evaluation, including the enabled state, variation, reason, and associated metadata.
/// </summary>
/// <remarks>This class encapsulates the outcome of an evaluation process, providing details about whether the
/// feature or condition being evaluated is enabled, the variation associated with the result, the reason for the
/// evaluation outcome, and any additional metadata. The metadata can be extended with contextual information such as
/// tenant or user identifiers.</remarks>
/// <param name="isEnabled"></param>
/// <param name="variation"></param>
/// <param name="reason"></param>
/// <param name="metadata"></param>
public class EvaluationResult(bool isEnabled, string? variation = default, string? reason = default,
	Dictionary<string, object>? metadata = default)
{
	public bool IsEnabled { get; } = isEnabled;
	public string Variation { get; } = variation ?? "off";
	public string Reason { get; } = reason ?? "";
	public Dictionary<string, object> Metadata { get; } = metadata ?? [];

	// Convenience methods for adding evaluation context to metadata
	public void AddTenant(string? tenantId)
	{
		if (!string.IsNullOrEmpty(tenantId))
		{
			Metadata["tenantId"] = tenantId!;
		}
	}

	public void AddUser(string? userId)
	{
		if (!string.IsNullOrEmpty(userId))
		{
			Metadata["userId"] = userId!;
		}
	}
}
