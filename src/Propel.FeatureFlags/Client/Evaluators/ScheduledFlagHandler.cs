using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators
{
	public sealed class ScheduledFlagHandler: FlagEvaluationHandlerBase<ScheduledFlagHandler>
	{
		protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			return flag.Status is FeatureFlagStatus.Scheduled
				or FeatureFlagStatus.ScheduledWithPercentage
				or FeatureFlagStatus.ScheduledWithPercentageAndUserTargeting
				or FeatureFlagStatus.ScheduledWithTimeWindow
				or FeatureFlagStatus.ScheduledWithTimeWindowAndPercentage
				or FeatureFlagStatus.ScheduledWithTimeWindowAndPercentageAndUserTargeting
				or FeatureFlagStatus.ScheduledWithTimeWindowAndUserTargeting
				or FeatureFlagStatus.ScheduledWithUserTargeting;
		}

		protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;

			// Check if we're in the scheduled enable period
			if (flag.ScheduledEnableDate.HasValue && evaluationTime >= flag.ScheduledEnableDate.Value)
			{
				// Check if we've passed the disable date
				if (flag.ScheduledDisableDate.HasValue && evaluationTime >= flag.ScheduledDisableDate.Value)
				{
					return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Scheduled disable date passed");
				}

				return new EvaluationResult(isEnabled: true, variation: "on", reason: "Scheduled enable date reached");
			}

			return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Scheduled enable date not reached");
		}
	}
}
