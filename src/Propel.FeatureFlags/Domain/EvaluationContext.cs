using Knara.UtcStrict;

namespace Propel.FeatureFlags.Domain;

/// <summary>
/// Represents the context in which an evaluation is performed, including tenant, user, attributes, and evaluation time.
/// </summary>
/// <remarks>This class provides contextual information that can be used to customize or influence the outcome of
/// an evaluation. It includes optional identifiers for the tenant and user, a collection of custom attributes, and the
/// time at which the evaluation occurs.</remarks>
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