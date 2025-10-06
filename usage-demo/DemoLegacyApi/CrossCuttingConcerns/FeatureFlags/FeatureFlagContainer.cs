using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags
{
	public class FeatureFlagContainer
	{
		private static readonly Lazy<FeatureFlagContainer> _instance = 
			new Lazy<FeatureFlagContainer>(() => new FeatureFlagContainer(), true);

		public static FeatureFlagContainer Instance => _instance.Value;

		private readonly IFeatureFlagRepository _repository;
		private readonly IFeatureFlagCache _cache;
		private readonly object _factoryLock = new object();
		private readonly object _clientLock = new object();
		private volatile IApplicationFlagClient _client;
		private volatile IFeatureFlagFactory _factory;

		private FeatureFlagContainer()
		{
			_repository = new FeatureFlagInMemoryRepository();
		}

		public IFeatureFlagRepository GetRepository()
		{
			return _repository;
		}

		public IFeatureFlagFactory GetOrCreateFlagFactory()
		{
			if (_factory != null)
				return _factory;

			lock (_factoryLock)
			{
				if (_factory != null)
					return _factory;

				var allFlags = GetAllFlags();
				foreach (var flag in allFlags)
				{
					Console.WriteLine($"Registered flag: {flag.Key} - {flag.Name}");
				}

				_factory = new FeatureFlagFactory(allFlags);
				return _factory;
			}
		}

		public IApplicationFlagClient GetOrCreateFlagClient()
		{
			if (_client != null)
				return _client;

			lock (_clientLock)
			{
				if (_client != null)
					return _client;

				var evaluators = DefaultEvaluators.Create();
				var processor = new ApplicationFlagProcessor(_repository, evaluators);

				_client = new ApplicationFlagClient(processor: processor);
				return _client;
			}
		}

		private IEnumerable<IFeatureFlag> GetAllFlags()
		{
			var flags = new List<IFeatureFlag>();

			var currentAssembly = Assembly.GetEntryAssembly() 
				?? Assembly.GetCallingAssembly()
				?? Assembly.GetExecutingAssembly();

			var allFlags = currentAssembly
				.GetTypes()
				.Where(t => typeof(IFeatureFlag).IsAssignableFrom(t)
						&& !t.IsInterface
						&& !t.IsAbstract);

			foreach (var flag in allFlags)
			{
				var instance = (IFeatureFlag)Activator.CreateInstance(flag)!;
				flags.Add(instance);
			}

			return flags;
		}
	}
}