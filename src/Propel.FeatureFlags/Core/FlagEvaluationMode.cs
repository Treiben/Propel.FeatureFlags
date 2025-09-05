namespace Propel.FeatureFlags.Core;

public enum FlagEvaluationMode
{
	Disabled = 0,
	Enabled = 1,
	Scheduled = 2,
	TimeWindow = 3,
	UserTargeted = 4,
	UserRolloutPercentage = 5,
	TenantRolloutPercentage = 6,
}

public class FlagEvaluationModeSet
{
	public FlagEvaluationMode[] EvaluationModes { get; private set; } = [FlagEvaluationMode.Disabled];

	public FlagEvaluationModeSet(FlagEvaluationMode[]? evaluationModes = null)
	{
		if (evaluationModes == null || evaluationModes.Length == 0)
		{
			EvaluationModes = [FlagEvaluationMode.Disabled];
		}
		else
		{
			foreach (var mode in evaluationModes)
			{
				AddMode(mode);
			}
		}
	}

	public static FlagEvaluationModeSet FlagIsDisabled => new([FlagEvaluationMode.Disabled]);

	public void AddMode(FlagEvaluationMode mode)
	{
		if (mode == FlagEvaluationMode.Disabled)
		{
			// If adding Disabled, it should be the only status
			EvaluationModes = [FlagEvaluationMode.Disabled];
			return;
		}

		if (mode != FlagEvaluationMode.Disabled && EvaluationModes.Contains(FlagEvaluationMode.Disabled))
		{
			// If adding any status other than Disabled, remove Disabled
			EvaluationModes = [.. EvaluationModes.Where(s => s != FlagEvaluationMode.Disabled)];
		}

		if (!EvaluationModes.Contains(mode))
		{
			EvaluationModes = [.. EvaluationModes, mode];
		}
	}

	public void RemoveMode(FlagEvaluationMode mode)
	{
		if (EvaluationModes.Contains(mode))
		{
			EvaluationModes = [.. EvaluationModes.Where(s => s != mode)];
		}
	}

	public bool ContainsModes(FlagEvaluationMode[] evaluationModes, bool any = true)
	{
		if (any)
			return evaluationModes.Any(s => EvaluationModes.Contains(s));
		else
			return evaluationModes.All(s => EvaluationModes.Contains(s));
	}
}

//public enum FeatureFlagStatus
//{
//	Disabled = 0,
//	Enabled = 1,
//	Scheduled = 2,
//	TimeWindow = 3,
//	ScheduledWithTimeWindow = 5,
//	Percentage = 6,
//	ScheduledWithPercentage = 8,
//	TimeWindowWithPercentage = 9,
//	ScheduledWithTimeWindowAndPercentage = 11,
//	UserTargeted = 12,
//	ScheduledWithUserTargeting = 14,
//	TimeWindowWithUserTargeting = 15,
//	ScheduledWithTimeWindowAndUserTargeting = 17,
//	PercentageWithUserTargeting = 18,
//	ScheduledWithPercentageAndUserTargeting = 20,
//	TimeWindowWithPercentageAndUserTargeting = 21,
//	ScheduledWithTimeWindowAndPercentageAndUserTargeting = 23
//}
