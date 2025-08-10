using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Client;
using Propel.FeatureFlags.Core;

namespace Propel.FeatureFlags.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddFeatureFlags(this IServiceCollection services, FlagOptions options)
	{
		// Register core services
		services.AddSingleton<IFeatureFlagEvaluator, FeatureFlagEvaluator>();
		services.AddSingleton<IFeatureFlagClient, FeatureFlagClient>();

		// Register chain builder and context evaluator
		services.AddSingleton(_ => EvaluatorChainBuilder.BuildChain());

		if (options.UseCache == true && string.IsNullOrEmpty(options.RedisConnectionString))
			services.AddSingleton<IFeatureFlagCache, MemoryFeatureFlagCache>();

		return services;
	}

}
