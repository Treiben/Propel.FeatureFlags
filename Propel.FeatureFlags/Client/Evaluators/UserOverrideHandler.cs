using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.Client.Evaluators
{
	public sealed class UserOverrideHandler : FlagEvaluationHandlerBase<UserOverrideHandler>
	{
		protected override bool CanProcess(FeatureFlag flag, EvaluationContext context)
		{
			// Check user overrides if user ID is provided
			return !string.IsNullOrEmpty(context.UserId);
		}

		protected override async Task<EvaluationResult?> ProcessEvaluation(FeatureFlag flag, EvaluationContext context)
		{
			var userId = context.UserId!;

			// Check explicit user disabled list
			if (flag.DisabledUsers.Contains(userId))
			{
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "User explicitly disabled");
			}

			// Check explicit user enabled list
			if (flag.EnabledUsers.Contains(userId))
			{
				return new EvaluationResult(isEnabled: true, variation: "on", reason: "User explicitly enabled");
			}

			// User not in any override lists, continue to next evaluator
			return await CallNext(flag, context);
		}
	}
}