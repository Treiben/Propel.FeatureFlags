using DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
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
	internal class FeatureFlagContainer : IDisposable
	{
		private static readonly Lazy<FeatureFlagContainer> _instance = 
			new Lazy<FeatureFlagContainer>(() => new FeatureFlagContainer(), true);

		public static FeatureFlagContainer Instance => _instance.Value;

		// Keep a persistent connection alive for in-memory database
		private static SqliteConnection _persistentConnection;

		private readonly SqliteFeatureFlagRepository _repository;
		private readonly SqliteDatabaseInitializer _databaseInitializer;

		private readonly IMemoryCache _memoryCache;
		private readonly IFeatureFlagCache _localCache;

		private readonly object _factoryLock = new object();
		private readonly object _clientLock = new object();
		private volatile IApplicationFlagClient _client;
		private volatile IFeatureFlagFactory _factory;

		private FeatureFlagContainer()
		{
			// For in-memory database, we must keep one connection open
			// Otherwise the database disappears when all connections close
			_persistentConnection = new SqliteConnection(WebApiApplication.InMemoryConnectionString);
			_persistentConnection.Open();

			_repository = new SqliteFeatureFlagRepository(_persistentConnection);
			_databaseInitializer = new SqliteDatabaseInitializer(_persistentConnection);

			_memoryCache = new MemoryCache(new MemoryCacheOptions
				{
					SizeLimit = 1024, // Size limit in units (not bytes)
					CompactionPercentage = 0.25 // Compact 25% when limit reached
				});

			_localCache = new LocalFeatureFlagCache(_memoryCache,
				new LocalCacheConfiguration()
				{
					CacheDurationInMinutes = 15,
					SlidingDurationInMinutes = 2,
				});
		}

		public IFeatureFlagRepository GetRepository()
		{
			return _repository;
		}

		public SqliteDatabaseInitializer GetDatabaseInitializer()
		{
			return _databaseInitializer;
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
				var processor = new ApplicationFlagProcessor(_repository, evaluators, _localCache);

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
				var instance = (IFeatureFlag)Activator.CreateInstance(flag);
				flags.Add(instance);
			}

			return flags;
		}

		public static void CloseDatabase()
		{
			_persistentConnection?.Close();
			_persistentConnection?.Dispose();
		}

		public void Dispose()
		{
			_databaseInitializer?.Dispose();

			_repository?.Dispose();

			_memoryCache?.Dispose();
		}
	}
}