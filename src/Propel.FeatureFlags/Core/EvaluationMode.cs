namespace Propel.FeatureFlags.Core;

public enum EvaluationMode
{
	Disabled = 0,
	Enabled = 1,
	Scheduled = 2,
	TimeWindow = 3,
	UserTargeted = 4,
	UserRolloutPercentage = 5,
	TenantRolloutPercentage = 6,
	TenantTargeted = 7,
	TargetingRules = 8,
}

public class EvaluationModes
{
	public EvaluationMode[] Modes { get; private set; } = [EvaluationMode.Disabled];

	public EvaluationModes(EvaluationMode[]? evaluationModes = null)
	{
		if (evaluationModes == null || evaluationModes.Length == 0)
		{
			Modes = [EvaluationMode.Disabled];
		}
		else
		{
			foreach (var mode in evaluationModes)
			{
				AddMode(mode);
			}
		}
	}

	public static EvaluationModes FlagIsDisabled => new([EvaluationMode.Disabled]);

	public void AddMode(EvaluationMode mode)
	{
		if (mode == EvaluationMode.Disabled)
		{
			// If adding Disabled, it should be the only status
			Modes = [EvaluationMode.Disabled];
			return;
		}

		if (mode != EvaluationMode.Disabled && Modes.Contains(EvaluationMode.Disabled))
		{
			// If adding any status other than Disabled, remove Disabled
			Modes = [.. Modes.Where(s => s != EvaluationMode.Disabled)];
		}

		if (!Modes.Contains(mode))
		{
			Modes = [.. Modes, mode];
		}
	}

	public void RemoveMode(EvaluationMode mode)
	{
		if (Modes.Contains(mode))
		{
			Modes = [.. Modes.Where(s => s != mode)];
		}

		if (Modes.Length == 0)
		{
			// If no modes left, default to Disabled
			Modes = [EvaluationMode.Disabled];
		}
	}

	public bool ContainsModes(EvaluationMode[] evaluationModes, bool any = true)
	{
		if (any)
			return evaluationModes.Any(s => Modes.Contains(s));
		else
			return evaluationModes.All(s => Modes.Contains(s));
	}
}
