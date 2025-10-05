using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using System.Reflection;

namespace Propel.FeatureFlags.PostgreSql;

internal static class CoreServicesExtensions
{
	public static IServiceCollection AddFeatureFlagServices(this IServiceCollection services, PropelConfiguration options)
	{
		services.AddSingleton(options);

		services.AddSingleton<IApplicationFlagProcessor, ApplicationFlagProcessor>();
		services.AddSingleton<IApplicationFlagClient, ApplicationFlagClient>();

		services.AddSingleton<IGlobalFlagClientService, GlobalFlagClientService>();
		services.AddSingleton<IGlobalFlagClient, GlobalFlagClient>();

		// Register evaluation manager with all handlers
		services.RegisterEvaluators();

		return services;
	}

	public static IServiceCollection AddInMemoryCache(this IServiceCollection services)
	{
		services.AddMemoryCache();
		services.TryAddSingleton<IFeatureFlagCache, InMemoryFlagCache>();
		return services;
	}

	public static IServiceCollection RegisterEvaluators(this IServiceCollection services)
	{
		// Register evaluation manager with all handlers
		services.AddSingleton<IEvaluators>(_ => new AllEvaluators(
			new HashSet<IOptionsEvaluator>(
				[   new ActivationScheduleEvaluator(),
					new OperationalWindowEvaluator(),
					new TargetingRulesEvaluator(),
					new TenantRolloutEvaluator(),
					new TerminalStateEvaluator(),
					new UserRolloutEvaluator(),
				])));
		return services;
	}

	public static IServiceCollection RegisterFlagsFromExecutingAssembly(this IServiceCollection services)
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