using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Services.Evaluation;

public interface IOrderedEvaluator
{
	EvaluationOrder EvaluationOrder { get; }
	bool CanProcess(FeatureFlag flag, EvaluationContext context);
	Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context);
}

public abstract class OrderedEvaluatorBase : IOrderedEvaluator
{
	public abstract EvaluationOrder EvaluationOrder { get; }
	public abstract bool CanProcess(FeatureFlag flag, EvaluationContext context);
	public abstract Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context);

	public EvaluationResult CreateEvaluationResult(FeatureFlag flag, EvaluationContext context, bool isActive, string because)
	{
		if (isActive)
		{
			var id = context.TenantId ?? context.UserId ?? "anonymous";
			var selectedVariation = flag.Variations.SelectVariationFor(flag.Key, id)
				?? flag.Variations.DefaultVariation;

			return new EvaluationResult(isEnabled: true,
				variation: selectedVariation, reason: because);
		}

		return new EvaluationResult(isEnabled: false,
			variation: flag.Variations.DefaultVariation, reason: because);
	}
}

public enum EvaluationOrder
{
	TenantRollout = 1,
	UserRollout = 2,
	ActivationSchedule = 3,
	OperationalWindow = 4,
	CustomTargeting = 5,
	Terminal = 99
}
