using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddFeatureFlags(this IServiceCollection services, FlagOptions options)
	{
		// Register core services
		services.AddSingleton<IFeatureFlagEvaluator, FeatureFlagEvaluator>();
		services.AddSingleton<IFeatureFlagClient, FeatureFlagClient>();

		// Register chain builder with all handlers
		//services.AddSingleton<IChainableEvaluationHandler>(_ => EvaluatorChainBuilder.BuildChain());

		// Register evaluation manager with all handlers
		services.AddSingleton(_ => new FlagEvaluationManager(
			[	new TenantOverrideHandler(),
				new UserOverrideHandler(),
				new ScheduledFlagHandler(),
				new TimeWindowFlagHandler(),
				new TargetedFlagHandler(),
				new UserPercentageHandler(),
				new StatusBasedFlagHandler()
			]));

		if (options.UseCache == true && string.IsNullOrEmpty(options.RedisConnectionString))
			services.TryAddSingleton<IFeatureFlagCache, MemoryFeatureFlagCache>();

		return services;
	}

}
