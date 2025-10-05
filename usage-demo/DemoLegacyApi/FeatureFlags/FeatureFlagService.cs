using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DemoLegacyApi.FeatureFlags
{
	//=================================================================================
	// In-Memory Flag Service (Implements IApplicationFlagService)
	//=================================================================================

	public class FeatureFlagService
	{
		private readonly IApplicationFlagClient _flagClient;
		private readonly IFeatureFlagFactory _flagFactory;

		public FeatureFlagService(IApplicationFlagClient client = null)
		{
			_flagFactory = FeatureFlagContainer.Instance.GetOrCreateFlagFactory();
			_flagClient = client ?? FeatureFlagContainer.Instance.GetOrCreateFlagClient();
		}

		public async Task<bool> IsEnabledAsync(IFeatureFlag flag,
				string? tenantId = null,
				string? userId = null,
				Dictionary<string, object>? attributes = null,
				System.Threading.CancellationToken cancellationToken = default)
		{
			var result = await _flagClient.EvaluateAsync(flag, userId: userId);
			return result?.IsEnabled ?? false;
		}

		public async Task<bool> IsEnabledAsync<T>(
				string? tenantId = null,
				string? userId = null,
				Dictionary<string, object>? attributes = null,
				System.Threading.CancellationToken cancellationToken = default)
			where T : IFeatureFlag
		{
			var flag = _flagFactory.GetFlagByType<T>();
			var result = await _flagClient.EvaluateAsync(flag, userId: userId);
			return result?.IsEnabled ?? false;
		}

		public async Task<bool> IsEnabledAsync(string flagKey,
				string? tenantId = null,
				string? userId = null,
				Dictionary<string, object>? attributes = null,
				System.Threading.CancellationToken cancellationToken = default)
		{
			var flag = _flagFactory.GetFlagByKey(flagKey);
			if (flag == null)
				throw new System.Exception($"Feature flag with key '{flagKey}' not found in the factory.");

			var result = await _flagClient.EvaluateAsync(flag, userId: userId);
			return result?.IsEnabled ?? false;
		}

		public async Task<string> GetVariationAsync(IFeatureFlag flag, 
				string defaultVariation,
				string? tenantId = null,
				string? userId = null,
				Dictionary<string, object>? attributes = null,
				System.Threading.CancellationToken cancellationToken = default)
		{
			var result = await _flagClient.GetVariationAsync(flag,
				defaultValue: defaultVariation, 
				userId: userId, 
				attributes: attributes
				);

			return result ?? throw new System.Exception($"Failed to get variation for flag {flag.Key}");
		}

		public async Task<EvaluationResult> EvaluateAsync(IFeatureFlag flag, 
			System.Threading.CancellationToken cancellationToken = default)
		{
			// Evaluate without user or attributes - in real scenarios, you would likely evaluation options
			// such as user ID or tenant ID or attributes for targeting and variation evaluation
			var result = await _flagClient.EvaluateAsync(flag);
			return result;
		}

		public IEnumerable<IFeatureFlag> GetAllFlags()
		{
			return _flagFactory.GetAllFlags();
		}
	}
}