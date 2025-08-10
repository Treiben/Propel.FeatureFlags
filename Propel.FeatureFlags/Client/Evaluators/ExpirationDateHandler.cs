using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators
{
	public sealed class ExpirationDateHandler : FlagEvaluationHandlerBase<ExpirationDateHandler>
	{
		protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
			return flag.ExpirationDate.HasValue && flag.ExpirationDate.Value != default && evaluationTime > flag.ExpirationDate.Value;
		}

		protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;

			return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation,
				reason: $"Flag {flag.Key} expired at {flag.ExpirationDate!.Value}, current time {evaluationTime}");
		}
	}
}
