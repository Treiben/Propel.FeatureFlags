namespace Propel.FeatureFlags.Domain;

public enum EvaluationMode
{
	Off = 0,
	On = 1,
	Scheduled = 2,
	TimeWindow = 3,
	UserTargeted = 4,
	UserRolloutPercentage = 5,
	TenantRolloutPercentage = 6,
	TenantTargeted = 7,
	TargetingRules = 8,
}

public class ModeSet
{
	public HashSet<EvaluationMode> Modes { get; } = [EvaluationMode.Off];

	public ModeSet(HashSet<EvaluationMode> modes)
	{
		if (modes.Count == 0 || modes.Contains(EvaluationMode.Off))
		{
			Modes = [EvaluationMode.Off];
		}
		else
			Modes = modes;
	}

	public bool Contains(EvaluationMode[] evaluationModes, bool any = true)
	{
		if (any)
			return evaluationModes.Any(mode => Modes.Contains(mode));
		else
			return evaluationModes.All(mode => Modes.Contains(mode));
	}

	public static implicit operator ModeSet(EvaluationMode mode) => new([mode]);
	public static implicit operator ModeSet(EvaluationMode[] modes) => new([.. modes]);
	public static implicit operator EvaluationMode[](ModeSet modes) => [.. modes.Modes];
}
