using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluationServices;
using Propel.FeatureFlags.FlagEvaluationServices.ApplicationScope;
using Propel.FeatureFlags.FlagEvaluationServices.GlobalScope;
using Propel.FeatureFlags.FlagEvaluators;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Services.GlobalScope;
using System.Reflection;

namespace Propel.FeatureFlags.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddFeatureFlagServices(this IServiceCollection services, PropelOptions options)
	{
		services.AddSingleton(options);

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

		return services;
	}

	public static IServiceCollection AddFeatureFlagDefaultCache(this IServiceCollection services)
	{
		services.AddMemoryCache();
		services.TryAddSingleton<IFeatureFlagCache, InMemoryFlagCache>();
		return services;
	}

	public static IServiceCollection RegisterAllFlags(this IServiceCollection services)
	{
		var currentAssembly = Assembly.GetEntryAssembly();

		var allFlags = currentAssembly
			.GetTypes()
			.Where(t => typeof(IFeatureFlag).IsAssignableFrom(t)
					&& !t.IsInterface
					&& !t.IsAbstract);

		foreach (var flag in allFlags)
		{
			var instance = (IFeatureFlag)Activator.CreateInstance(flag)!;
			services.AddSingleton(instance);
		}

		return services;
	}
}