using Propel.FeatureFlags.Helpers;

namespace Propel.FeatureFlags.Domain;

public class EvaluationContext
{
	public EvaluationContext(
					string? tenantId = null,
					string? userId = null, 
					Dictionary<string, object>? attributes = null,
					DateTime? evaluationTime = null, 
					string? timeZone = "UTC")
	{
		TenantId = tenantId;
		UserId = userId;
		Attributes = attributes ?? [];
		EvaluationTime = evaluationTime == null ? DateTime.UtcNow : DateTimeHelpers.NormalizeToUtc(evaluationTime!);
		TimeZone = timeZone;
	}

	public string? TenantId { get; }			// Primary evaluation context
	public string? UserId { get; }				// Secondary evaluation context
	public Dictionary<string, object> Attributes { get; }
	public DateTime? EvaluationTime { get; }
	public string? TimeZone { get; }
}