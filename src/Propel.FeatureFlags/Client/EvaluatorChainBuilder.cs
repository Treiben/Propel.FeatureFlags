using Propel.FeatureFlags.Client.Evaluators;

namespace Propel.FeatureFlags.Client;

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
		var head = new TenantOverrideHandler();
		var userOverride = new UserOverrideHandler();
		var scheduled = new ScheduledFlagHandler();
		var timeWindow = new TimeWindowFlagHandler();
		var targeted = new TargetedFlagHandler();
		var percentage = new UserPercentageHandler();
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