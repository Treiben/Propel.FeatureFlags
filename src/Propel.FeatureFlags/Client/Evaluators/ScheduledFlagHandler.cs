using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators
{
	public sealed class ScheduledFlagHandler: ChainableEvaluationHandler<ScheduledFlagHandler>, IOrderedEvaluationHandler
	{
		public int EvaluationOrder => 6;

		Task<EvaluationResult?> IOrderedEvaluationHandler.ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			return ProcessEvaluation(flag, context);
		}

		bool IOrderedEvaluationHandler.CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			return CanProcess(flag, context);
		}

		protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.Scheduled]);
		}

		protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
			var (isEnabled, reason) = flag.Schedule.IsEnabled(evaluationTime);

			return new EvaluationResult(isEnabled: isEnabled, 
				variation: isEnabled ? "on" : flag.Variations.DefaultVariation, reason: reason);
		}
	}
}
