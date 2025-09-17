using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Services;
using Propel.FeatureFlags.Services.ApplicationScope;
using Propel.FeatureFlags.Services.Evaluation;
using Propel.FeatureFlags.Services.GlobalScope;
using System.Reflection;

namespace Propel.FeatureFlags;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddFeatureFlagServices(this IServiceCollection services, FeatureFlagConfigurationOptions options)
	{
		// Register core services
		services.AddSingleton<CacheOptions>(options.CacheOptions ?? new CacheOptions { UseCache = false });
		services.AddSingleton<FeatureFlagConfigurationOptions>(options);

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

	public static IServiceCollection RegisterFlagsInDatabase(this IServiceCollection services)
	{
		var currentAssembly = Assembly.GetExecutingAssembly();

		var allFlags = currentAssembly
			.GetTypes()
			.Where(t => typeof(IRegisteredFeatureFlag).IsAssignableFrom(t)
					&& !t.IsInterface
					&& !t.IsAbstract);

		foreach (var flag in allFlags)
		{
			var sliceInstance = (IRegisteredFeatureFlag)Activator.CreateInstance(flag)!;
			services.AddSingleton(typeof(IRegisteredFeatureFlag), flag);
		}

		return services;
	}
}
