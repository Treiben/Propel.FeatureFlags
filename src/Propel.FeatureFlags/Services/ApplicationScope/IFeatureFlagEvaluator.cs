using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using System.Text.Json;

namespace Propel.FeatureFlags.Services.ApplicationScope;

public interface IFeatureFlagEvaluator
{
	Task<EvaluationResult?> Evaluate(IApplicationFeatureFlag flag, EvaluationContext context, CancellationToken cancellationToken = default);
	Task<T> GetVariation<T>(IApplicationFeatureFlag flag, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default);
}

public sealed class FeatureFlagEvaluator(
	IFeatureFlagRepository repository,
	IFlagEvaluationManager evaluationManager,
	IFeatureFlagCache? cache = null) : IFeatureFlagEvaluator
{
	private readonly IFeatureFlagRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IFlagEvaluationManager _evaluationManager = evaluationManager ?? throw new ArgumentNullException(nameof(evaluationManager));

	private string ApplicationName => ApplicationInfo.Name;
	private string ApplicationVersion => ApplicationInfo.Version;

	public async Task<EvaluationResult?> Evaluate(IApplicationFeatureFlag applicationFlag, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		var flag = await GetFlagAsync(applicationFlag.Key, cancellationToken);
		if (flag == null)
		{
			// Auto-create flag in disabled state for deployment scenarios
			await CreateDefaultFlagAsync(applicationFlag, cancellationToken);
			return new EvaluationResult
			(
				isEnabled: applicationFlag.DefaultMode == EvaluationMode.Enabled,
				reason: $"A new flag created in the database with specified mode {applicationFlag.DefaultMode}"
			);
		}

		return await _evaluationManager.ProcessEvaluation(flag, context);
	}

	public async Task<T> GetVariation<T>(IApplicationFeatureFlag applicationFlag, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			// Get flag once and reuse for both evaluation and variation lookup
			var flag = await GetFlagAsync(applicationFlag.Key, cancellationToken);
			if (flag == null)
			{
				// Auto-create and return default
				await CreateDefaultFlagAsync(applicationFlag, cancellationToken);
				return defaultValue;
			}

			// Evaluate using the already-fetched flag
			var result = await _evaluationManager.ProcessEvaluation(flag, context);
			if (result?.IsEnabled == false)
			{
				return defaultValue;
			}

			// Use the same flag instance for variation lookup
			if (flag.Variations.Values.TryGetValue(result!.Variation, out var variationValue))
			{
				if (variationValue is JsonElement jsonElement)
				{
					var deserializedValue = JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), JsonDefaults.JsonOptions);
					return deserializedValue ?? defaultValue;
				}

				if (variationValue is T directValue)
				{
					return directValue;
				}

				// Try to convert
				var convertedValue = (T)Convert.ChangeType(variationValue, typeof(T));
				return convertedValue ?? defaultValue;
			}

			return defaultValue;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return defaultValue;
		}
	}

	private async Task<FeatureFlag?> GetFlagAsync(string flagKey, CancellationToken cancellationToken)
	{
		FeatureFlag? flag = null;

		// Try cache first
		var cacheKey = new ApplicationCacheKey(flagKey, ApplicationName, ApplicationVersion);
		if (cache != null)
		{
			flag = await cache.GetAsync(cacheKey, cancellationToken);
		}

		// If not in cache, get from repository
		if (flag == null)
		{
			flag = await _repository.GetAsync(new FlagKey(
				key: flagKey,
				scope: Scope.Application,
				applicationName: ApplicationName,
				applicationVersion: ApplicationVersion
			), cancellationToken);
			
			// Cache for future requests if found
			if (flag != null && cache != null)
			{
				await cache.SetAsync(cacheKey, flag, cancellationToken);
			}
		}

		return flag;
	}

	private async Task CreateDefaultFlagAsync(IApplicationFeatureFlag applicationFlag, CancellationToken cancellationToken)
	{
		var flagKey = applicationFlag.Key;
		var defaultFlag = new FeatureFlag
		{
			Key = flagKey,
			Name = applicationFlag.Name ?? flagKey, // Keep original name for display purposes
			Description = applicationFlag.Description ?? $"Auto-created flag for {flagKey} in application {ApplicationName}",
			Retention = new RetentionPolicy
			(
				isPermanent: false,
				expirationDate: DateTime.UtcNow.AddDays(30),
				scope: Scope.Application,
				applicationName: ApplicationName,
				applicationVersion: ApplicationVersion
			),
		};

		if (applicationFlag.DefaultMode == EvaluationMode.Enabled)
		{
			defaultFlag.ActiveEvaluationModes.AddMode(EvaluationMode.Enabled);
		}

		try
		{
			// Save to repository and return the created flag (repository may set additional properties)
			var createdFlag = await _repository.CreateAsync(defaultFlag, cancellationToken);
			// Cache for future requests
			if (cache != null)
			{
				// Create composite key for uniqueness per application
				var cacheKey = new ApplicationCacheKey(flagKey, ApplicationName, ApplicationVersion);
				await cache.SetAsync(cacheKey, createdFlag, cancellationToken);
			}
		}
		catch (Exception ex)
		{
			// Even if repository save fails, just return
			// This handles scenarios where repository might be temporarily unavailable
			// The flag will be attempted to be created again on next evaluation
		}
	}
}