using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.Evaluation.ApplicationScope;
using Propel.FeatureFlags.Evaluation.GlobalScope;
using Propel.FeatureFlags.Evaluation.Strategies;

namespace Propel.FeatureFlags;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddFeatureFlags(this IServiceCollection services, FeatureFlagConfigurationOptions options)
	{
		// Register core services
		services.AddSingleton<IFeatureFlagEvaluator, FeatureFlagEvaluator>();
		services.AddSingleton<IFeatureFlagClient, FeatureFlagClient>();

		services.AddSingleton<IGlobalFlagEvaluator, GlobalFlagEvaluator>();
		services.AddSingleton<IGlobalFlagClient, GlobalFlagClient>();

		// Register evaluation manager with all handlers
		services.AddSingleton<IFlagEvaluationManager>(_ => new FlagEvaluationManager(
			new HashSet<IOrderedEvaluator>(
				[	new ActivationScheduleEvaluator(),
					new OperationalWindowEvaluator(),
					new TargetingRulesEvaluator(),
					new TenantRolloutEvaluator(),
					new TerminalStateEvaluator(),
					new UserRolloutEvaluator(),
				])));

		if (options.UseCache == true && string.IsNullOrEmpty(options.RedisConnectionString))
			services.TryAddSingleton<IFeatureFlagCache, MemoryFeatureFlagCache>();

		return services;
	}

}
