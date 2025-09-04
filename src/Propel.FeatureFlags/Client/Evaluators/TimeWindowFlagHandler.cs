using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators;

public sealed class TimeWindowFlagHandler : ChainableEvaluationHandler<TimeWindowFlagHandler>, IOrderedEvaluationHandler
{
	public int EvaluationOrder => 4;

	bool IOrderedEvaluationHandler.CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return CanProcess(flag, context);
	}

	Task<EvaluationResult?> IOrderedEvaluationHandler.ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		return ProcessEvaluation(flag, context);
	}

	protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
	{
		return flag.EvaluationModeSet.ContainsModes([FlagEvaluationMode.TimeWindow]);
	}

	protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		if (!flag.TimeWindow.IsSet())
		{
			return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "Time window not configured");
		}

		// Convert to specified timezone
		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
		var userTimeZone = !string.IsNullOrEmpty(context.TimeZone) ? context.TimeZone : flag.TimeWindow.GetTimeZone();

		var (currentTime, currentDay) = TimeZoneHelper.GetCurrentTimeAndDayInTimeZone(userTimeZone!, evaluationTime);

		// Check if current day is in allowed days
		if (flag.TimeWindow.InWindowDays(currentDay))
		{
			return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "Outside allowed days");
		}

		// Check time window
		bool inWindow = flag.TimeWindow.InWindowTime(currentTime);

		return new EvaluationResult(isEnabled: inWindow, 
			variation: inWindow ? "on" : flag.Variations.DefaultVariation, 
			reason: inWindow ? "Within time window" : "Outside time window");
	}
}
