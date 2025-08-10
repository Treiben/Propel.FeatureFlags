using Propel.FeatureFlags.Client.Evaluators;

namespace Propel.FeatureFlags.Client
{
    public sealed class EvaluatorChainBuilder
    {
        public static IFlagEvaluationHandler BuildChain()
        {
            // Build the chain in order of priority:
            // 1. Check flag expiration
            // 2. Tenant overrides (highest priority)
            // 3. User overrides
            // 4. Status-specific evaluators (Scheduled, TimeWindow, UserTargeted, Percentage)
            // 5. Basic status evaluator (Disabled, Enabled)

            // Create evaluators manually without IServiceProvider
            var head = new ExpirationDateHandler();
			var tenantOverride = new TenantOverrideHandler();
            var userOverride = new UserOverrideHandler();
            var scheduled = new ScheduledFlagHandler();
            var timeWindow = new TimeWindowFlagHandler();
            var targeted = new TargetedFlagHandler();
            var percentage = new UserPercentageHandler();
            var statusBased = new StatusBasedFlagHandler();

            // Link the chain
            head.NextHandler = tenantOverride;
			tenantOverride.NextHandler = userOverride;
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
        public static IFlagEvaluationHandler CreateDefaultChain()
        {
            return EvaluatorChainBuilder.BuildChain();
        }

        public static IFlagEvaluationHandler CreateCustomChain(params IFlagEvaluationHandler[] evaluators)
        {
            if (evaluators == null || evaluators.Length == 0)
                return CreateDefaultChain();

            // Link the provided evaluators
            for (int i = 0; i < evaluators.Length - 1; i++)
            {
                evaluators[i].NextHandler = evaluators[i + 1];
            }

            return evaluators[0];
        }
    }
}