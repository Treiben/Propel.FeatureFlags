using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators;

public interface IOrderedEvaluationHandler
{
	int EvaluationOrder { get; }

	bool CanProcess(FeatureFlag flag, EvaluationContext context);
	Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context);
}
