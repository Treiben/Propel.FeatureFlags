using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.FlagEvaluators;

public interface IOptionsEvaluator
{
	EvaluationOrder EvaluationOrder { get; }
	bool CanProcess(EvaluationOptions options, EvaluationContext context);
	ValueTask<EvaluationResult?> Evaluate(EvaluationOptions options, EvaluationContext context);
}

public abstract class EvaluatorBase : IOptionsEvaluator
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
