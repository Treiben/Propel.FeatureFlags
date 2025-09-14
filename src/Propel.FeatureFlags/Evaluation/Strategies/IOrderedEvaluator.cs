using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Strategies;

public interface IOrderedEvaluator
{
	EvaluationOrder EvaluationOrder { get; }
	bool CanProcess(FeatureFlag flag, EvaluationContext context);
	Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context);
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
