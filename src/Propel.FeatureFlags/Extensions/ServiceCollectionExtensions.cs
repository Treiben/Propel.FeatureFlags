using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Evaluation;
using Propel.FeatureFlags.FlagEvaluators;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Services;
using Propel.FeatureFlags.Services.ApplicationScope;
using Propel.FeatureFlags.Services.GlobalScope;
using System.Reflection;

namespace Propel.FeatureFlags.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPropelServices(this IServiceCollection services, PropelOptions options)
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

	public static IServiceCollection AddPropelInMemoryCache(this IServiceCollection services)
	{
		services.AddMemoryCache();
		services.TryAddSingleton<IFeatureFlagCache, MemoryFeatureFlagCache>();
		return services;
	}

	public static IServiceCollection AddPropelFeatureFlags(this IServiceCollection services)
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