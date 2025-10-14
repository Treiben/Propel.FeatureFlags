using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;
using Propel.FeatureFlags.Infrastructure;
using System.Reflection;

namespace Propel.FeatureFlags.DependencyInjection.Extensions;

internal static class FeatureFlagServicesExtensions
{
	internal static IServiceCollection AddFeatureFlagServices(this IServiceCollection services, PropelConfiguration propelConfiguration)
	{
		services.AddSingleton(propelConfiguration);

		services.AddSingleton<IApplicationFlagProcessor, ApplicationFlagProcessor>();
		services.AddSingleton<IApplicationFlagClient, ApplicationFlagClient>();

		services.AddSingleton<IGlobalFlagProcessor, GlobalFlagProcessor>();
		services.AddSingleton<IGlobalFlagClient, GlobalFlagClient>();

		// Register evaluation manager with all handlers
		services.RegisterEvaluators();

		return services;
	}

	internal static IServiceCollection RegisterEvaluators(this IServiceCollection services)
	{
		// Register evaluation manager with all handlers
		services.AddSingleton<IEvaluatorsSet>(_ => new EvaluatorsSet(
			new HashSet<IEvaluator>(
				[   new ActivationScheduleEvaluator(),
					new OperationalWindowEvaluator(),
					new TargetingRulesEvaluator(),
					new TenantRolloutEvaluator(),
					new TerminalStateEvaluator(),
					new UserRolloutEvaluator(),
				])));
		return services;
	}

	internal static IServiceCollection RegisterFlagsFromExecutingAssembly(this IServiceCollection services)
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