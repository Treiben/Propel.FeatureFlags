using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public sealed class UserOverrideHandler : ChainableEvaluationHandler<UserOverrideHandler>, IOrderedEvaluationHandler
{
	public int EvaluationOrder => 2;

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
		// Check user overrides if user ID is provided
		return !string.IsNullOrWhiteSpace(context.UserId);
	}

	protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
	{
		var userId = context.UserId!;

		if (!flag.Users.IsUserExplicitlySet(userId))
		{
			return await CallNext(flag, context);
		}

		return flag.Users.IsUserEnabled(userId)
			? new EvaluationResult(isEnabled: true, variation: "on", reason: "User explicitly enabled")
			: new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, reason: "User explicitly disabled");
	}
}