using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Evaluation.Handlers;

public interface IChainableEvaluationHandler
{
	IChainableEvaluationHandler? NextHandler { get; set; }
	Task<EvaluationResult?> Handle(FeatureFlag flag, EvaluationContext context);
}

public abstract class ChainableEvaluationHandler<T> : IChainableEvaluationHandler where T : class
{
	public IChainableEvaluationHandler? NextHandler { get; set; }

	public async Task<EvaluationResult?> Handle(FeatureFlag flag, EvaluationContext context)
	{
		// Check if this evaluator should handle this flag
		if (!CanProcess(flag, context))
		{
			return await CallNext(flag, context);
		}

		// Handle the evaluation
		var result = await ProcessEvaluation(flag, context);

		// If result is inconclusive and we have a next evaluator, continue the chain
		if (result == null && NextHandler != null)
		{
			return await NextHandler.Handle(flag, context);
		}

		// Return result or default fallback
		return result ?? new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, 
			reason: "No evaluator could handle this flag");
	}

	protected virtual async Task<EvaluationResult> CallNext(FeatureFlag flag, EvaluationContext context)
	{
		if (NextHandler != null)
		{
			return await NextHandler.Handle(flag, context);
		}

		return new EvaluationResult(isEnabled: false, variation: flag.Variations.DefaultVariation, 
			reason: "End of evaluation chain");
	}

	protected abstract bool CanProcess(FeatureFlag flag, EvaluationContext context);
	protected abstract Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context);
}

public sealed class EvaluatorChainBuilder
{
	public static IChainableEvaluationHandler BuildChain()
	{
		// Build the chain in order of priority:
		// 1. Check flag expiration <-- excluded because it is not a blocking evaluator: some flags might be expired but still used in the code base
		// 2. Tenant overrides (highest priority)
		// 3. User overrides (second priority)
		// 4. Combinational evaluator (Scheduled, TimeWindow, UserTargeted, Percentage)
		// 5. Status-specific evaluators (Scheduled, TimeWindow, UserTargeted, Percentage)
		// 6. Basic status evaluator (Disabled, Enabled)

		// Create evaluators manually without IServiceProvider
		//var head = new ExpirationDateHandler();
		var head = new TenantRolloutHandler();
		var userOverride = new UserOverrideHandler();
		var scheduled = new ScheduledFlagHandler();
		var timeWindow = new TimeWindowFlagHandler();
		var targeted = new TargetedFlagHandler();
		var percentage = new UserRolloutHandler();
		var statusBased = new StatusBasedFlagHandler();

		// Link the chain
		head.NextHandler = userOverride;
		userOverride.NextHandler = scheduled;
		scheduled.NextHandler = timeWindow;
		timeWindow.NextHandler = targeted;
		targeted.NextHandler = percentage;
		percentage.NextHandler = statusBased;
		// statusBased is the terminal evaluator (no NextEvaluator)

		return head;
	}
}

// Alternative factory pattern approach for even more flexibility
public sealed class EvaluatorChainFactory
{
	public static IChainableEvaluationHandler CreateDefaultChain()
	{
		return EvaluatorChainBuilder.BuildChain();
	}

	public static IChainableEvaluationHandler CreateCustomChain(params IChainableEvaluationHandler[] links)
	{
		if (links == null || links.Length == 0)
			return CreateDefaultChain();

		// Link the provided evaluators
		for (int i = 0; i < links.Length - 1; i++)
		{
			links[i].NextHandler = links[i + 1];
		}

		return links[0];
	}
}