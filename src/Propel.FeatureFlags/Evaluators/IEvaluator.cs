using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

/// <summary>
/// Defines the contract for an evaluator that processes input based on specified options and context.
/// </summary>
/// <remarks>Implementations of this interface are responsible for determining whether they can process a given
/// evaluation request and for performing the evaluation if applicable. The evaluation process is influenced by the
/// provided options and context.</remarks>
public interface IEvaluator
{
	EvaluationOrder EvaluationOrder { get; }
	bool CanProcess(EvaluationOptions options, EvaluationContext context);
	ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context);
}

/// <summary>
/// Serves as the base class for all evaluators, providing a framework for evaluating conditions and producing results
/// based on the specified options and context.
/// </summary>
/// <remarks>This abstract class defines the core contract for evaluators, including the evaluation order, the
/// ability to determine if a condition can be processed, and the evaluation logic itself. Implementations of this class
/// must provide concrete behavior for these operations.</remarks>
public abstract class EvaluatorBase : IEvaluator
{
	public abstract EvaluationOrder EvaluationOrder { get; }
	public abstract bool CanProcess(EvaluationOptions flag, EvaluationContext context);
	public abstract ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context);

	internal EvaluationResult CreateEvaluationResult(EvaluationOptions options, EvaluationContext context, bool isActive, string because)
	{
		if (isActive)
		{
			var id = context.TenantId ?? context.UserId ?? "anonymous";

			var selectedVariation = options.Variations.SelectVariationFor(options.Key, id)
				?? options.Variations.DefaultVariation;

			return new EvaluationResult(isEnabled: true,
				variation: selectedVariation, reason: because);
		}

		return new EvaluationResult(isEnabled: false,
			variation: options.Variations.DefaultVariation, reason: because);
	}

	internal EvaluationResult CreateEvaluationResult(EvaluationOptions options, bool isActive, string variation, string because)
	{
		if (isActive)
		{
			return new EvaluationResult(isEnabled: true,
				variation: variation, reason: because);
		}

		return new EvaluationResult(isEnabled: false,
			variation: options.Variations.DefaultVariation, reason: because);
	}
}

public enum EvaluationOrder
{
	TenantRollout = 1,
	UserRollout = 2,
	ActivationSchedule = 3,
	OperationalWindow = 4,
	CustomTargeting = 5,
	Terminal = 99
}

public class EvaluationOptionsArgumentException : ArgumentException
{
	public EvaluationOptionsArgumentException(): base(message: "Evaluation argument error")
	{
	}
	public EvaluationOptionsArgumentException(string paramName, string message = "") : base(paramName: paramName, message: message)
	{
	}
	public EvaluationOptionsArgumentException(string paramName, string? message, Exception? innerException) 
		: base(paramName: paramName, message: message, innerException: innerException)
	{
	}
}
