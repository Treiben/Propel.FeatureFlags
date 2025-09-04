using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public interface IOrderedEvaluationHandler
{
	int EvaluationOrder { get; }
	bool CanProcess(FeatureFlag flag, EvaluationContext context);
	Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context);
}
