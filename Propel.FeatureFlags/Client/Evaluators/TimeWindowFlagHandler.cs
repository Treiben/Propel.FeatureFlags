using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators
{
	public sealed class TimeWindowFlagHandler : FlagEvaluationHandlerBase<TimeWindowFlagHandler>
	{
		protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			return flag.Status == FeatureFlagStatus.TimeWindow;
		}

		protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			if (!flag.WindowStartTime.HasValue || !flag.WindowEndTime.HasValue)
			{
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Time window not configured");
			}

			// Convert to specified timezone
			var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
			var userTimeZone = !string.IsNullOrEmpty(context.TimeZone) ? context.TimeZone : flag.TimeZone ?? "UTC";

			var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(userTimeZone);
			if (evaluationTime.Kind != DateTimeKind.Utc)
			{
				evaluationTime = DateTime.SpecifyKind(evaluationTime, DateTimeKind.Unspecified);
			}
			var localTime = TimeZoneInfo.ConvertTimeFromUtc(evaluationTime, timeZoneInfo);
			var currentTime = localTime.TimeOfDay;
			var currentDay = localTime.DayOfWeek;

			// Check if current day is in allowed days
			if (flag.WindowDays?.Any() == true && !flag.WindowDays.Contains(currentDay))
			{
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Outside allowed days");
			}

			// Check time window
			bool inWindow;
			if (flag.WindowStartTime.Value <= flag.WindowEndTime.Value)
			{
				// Same day window (e.g., 9:00 - 17:00)
				inWindow = currentTime >= flag.WindowStartTime.Value && currentTime <= flag.WindowEndTime.Value;
			}
			else
			{
				// Overnight window (e.g., 22:00 - 06:00)
				inWindow = currentTime >= flag.WindowStartTime.Value || currentTime <= flag.WindowEndTime.Value;
			}

			return new EvaluationResult(isEnabled: inWindow, variation: inWindow ? "on" : flag.DefaultVariation, reason: inWindow ? "Within time window" : "Outside time window");
		}
	}
}
