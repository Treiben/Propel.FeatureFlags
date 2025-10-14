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

/// <summary>
/// Represents a collection of evaluation modes, providing utility methods for checking mode membership and implicit
/// conversions for ease of use.
/// </summary>
/// <remarks>The <see cref="ModeSet"/> class is designed to manage a set of <see cref="EvaluationMode"/> values.
/// It ensures that the set always contains valid modes, defaulting to <see cref="EvaluationMode.Off"/> if no modes are
/// provided or if <see cref="EvaluationMode.Off"/> is explicitly included. The class supports operations to check for
/// the presence of modes and provides implicit conversions to and from <see cref="EvaluationMode"/> and arrays of <see
/// cref="EvaluationMode"/>.</remarks>
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
