using Knara.UtcStrict;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags
{
	//=================================================================================
	// In-Memory Flag Store (Simulates Database/Repository)
	// In real applications, use a persistent repository implementation
	//=================================================================================

	public class FeatureFlagEntity
	{
		public string Key { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public string ApplicationName { get; set; }
		public string ApplicationVersion { get; set; }
		public Scope Scope { get; set; }
		public ModeSet ModeSet { get; set; }
		public UtcSchedule Schedule { get; set; }
		public UtcTimeWindow OperationalWindow { get; set; }
		public List<ITargetingRule> TargetingRules { get; set; }
		public AccessControl UserAccessControl { get; set; }
		public AccessControl TenantAccessControl { get; set; }
		// Variations for A/B testing
		public Variations Variations { get; set; }
	}

	public class FeatureFlagInMemoryRepository : IFeatureFlagRepository
	{
		private readonly List<FeatureFlagEntity> _flags;
		private readonly object _lock = new object();

		public FeatureFlagInMemoryRepository()
		{
			_flags = new List<FeatureFlagEntity>();
		}

		public Task<EvaluationOptions> GetEvaluationOptionsAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
		{
			lock (_lock)
			{
				var flag = _flags.FirstOrDefault(f =>
					f.Key == identifier.Key &&
					f.Scope == identifier.Scope &&
					(identifier.Scope == Scope.Global ||
					 (f.ApplicationName == identifier.ApplicationName &&
					  f.ApplicationVersion == identifier.ApplicationVersion)));

				if (flag == null)
				{
					return Task.FromResult<EvaluationOptions>(null);
				}

				var options = new EvaluationOptions(
					key: flag.Key,
					modeSet: flag.ModeSet,
					schedule: flag.Schedule,
					operationalWindow: flag.OperationalWindow,
					targetingRules: flag.TargetingRules,
					userAccessControl: flag.UserAccessControl,
					tenantAccessControl: flag.TenantAccessControl,
					variations: flag.Variations
				);

				return Task.FromResult(options);
			}
		}

		public Task CreateApplicationFlagAsync(FlagIdentifier identifier, EvaluationMode activeMode, string name, string description, CancellationToken cancellationToken = default)
		{
			if (identifier.Scope == Scope.Global)
			{
				throw new InvalidOperationException("Only application-level flags are allowed to be created from client applications. Global flags are outside of application domain and must be created by management tools.");
			}

			lock (_lock)
			{
				var existingFlag = _flags.FirstOrDefault(f =>
					f.Key == identifier.Key &&
					f.ApplicationName == identifier.ApplicationName &&
					f.ApplicationVersion == identifier.ApplicationVersion);

				if (existingFlag != null)
				{
					// Flag already exists, do nothing (similar to ON CONFLICT DO NOTHING in PostgreSQL)
					return Task.CompletedTask;
				}

				var modeSet = new ModeSet(new HashSet<EvaluationMode> { activeMode });

				var newFlag = new FeatureFlagEntity
				{
					Key = identifier.Key,
					Name = name,
					Description = description,
					ApplicationName = identifier.ApplicationName ?? string.Empty,
					ApplicationVersion = identifier.ApplicationVersion ?? "1.0.0.0",
					Scope = Scope.Application,
					ModeSet = modeSet,
					Schedule = null,
					OperationalWindow = null,
					TargetingRules = new List<ITargetingRule>(),
					UserAccessControl = AccessControl.Unrestricted,
					TenantAccessControl = AccessControl.Unrestricted,
					Variations = null
				};

				_flags.Add(newFlag);
			}

			return Task.CompletedTask;
		}
	}
}