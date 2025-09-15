using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Services;
using Propel.FeatureFlags.Services.ApplicationScope;
using Propel.FeatureFlags.Services.Evaluation;
using Propel.FeatureFlags.Services.GlobalScope;

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

		if (options.CacheOptions?.UseCache == true && string.IsNullOrEmpty(options.RedisConnectionString))
			services.TryAddSingleton<IFeatureFlagCache, MemoryFeatureFlagCache>();

		return services;
	}

}
