using Knara.UtcStrict;

namespace Propel.FeatureFlags.Domain;

public class EvaluationContext
{
	public EvaluationContext(
					string? tenantId = null,
					string? userId = null, 
					Dictionary<string, object>? attributes = null,
					UtcDateTime? evaluationTime = null)
	{
		TenantId = tenantId;
		UserId = userId;
		Attributes = attributes ?? [];
		EvaluationTime = evaluationTime ?? UtcDateTime.UtcNow;
	}

	public string? TenantId { get; }			// Primary evaluation context
	public string? UserId { get; }				// Secondary evaluation context
	public Dictionary<string, object> Attributes { get; }
	public UtcDateTime? EvaluationTime { get; }
}