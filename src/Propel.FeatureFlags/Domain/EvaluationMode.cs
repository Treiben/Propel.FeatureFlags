namespace Propel.FeatureFlags.Domain;

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
	public HashSet<EvaluationMode> Modes { get; } = [EvaluationMode.Disabled];

	public EvaluationModes(HashSet<EvaluationMode> modes)
	{
		if (modes.Count == 0)
		{
			Modes = [EvaluationMode.Disabled];
		}

		if (modes.Contains(EvaluationMode.Disabled) && modes.Count > 1)
		{
			// If Disabled is present with other modes, remove all other modes
			Modes = [EvaluationMode.Disabled];
		}

		Modes = modes;
	}

	public static EvaluationModes FlagIsDisabled => new([EvaluationMode.Disabled]);

	public void AddMode(EvaluationMode mode)
	{
		if (mode == EvaluationMode.Disabled)
		{
			// If adding Disabled, it should be the only status
			Modes.Clear();
			Modes.Add(EvaluationMode.Disabled);
			return;
		}

		if (mode != EvaluationMode.Disabled && Modes.Contains(EvaluationMode.Disabled))
		{
			// If adding any status other than Disabled, remove Disabled
			Modes.Remove(EvaluationMode.Disabled);
		}

		Modes.Add(mode);
	}

	public void RemoveMode(EvaluationMode mode)
	{
		Modes.Remove(mode);

		if (Modes.Count == 0)
		{
			// If no modes left, default to Disabled
			Modes.Add(EvaluationMode.Disabled);
		}
	}

	public bool ContainsModes(EvaluationMode[] evaluationModes, bool any = true)
	{
		if (any)
			return evaluationModes.Any(mode => Modes.Contains(mode));
		else
			return evaluationModes.All(mode => Modes.Contains(mode));
	}
}
