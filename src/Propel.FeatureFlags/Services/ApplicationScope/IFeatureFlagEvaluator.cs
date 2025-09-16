using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using System.Text.Json;

namespace Propel.FeatureFlags.Services.ApplicationScope;

public interface IFeatureFlagEvaluator
{
	Task<EvaluationResult?> Evaluate(IRegisteredFeatureFlag flag, EvaluationContext context, CancellationToken cancellationToken = default);
	Task<T> GetVariation<T>(IRegisteredFeatureFlag flag, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default);
}

public sealed class FeatureFlagEvaluator(
	IFlagEvaluationRepository repository,
	IFlagEvaluationManager evaluationManager,
	IFeatureFlagCache? cache = null) : IFeatureFlagEvaluator
{
	private readonly IFlagEvaluationRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IFlagEvaluationManager _evaluationManager = evaluationManager ?? throw new ArgumentNullException(nameof(evaluationManager));

	private string ApplicationName => ApplicationInfo.Name;
	private string ApplicationVersion => ApplicationInfo.Version;

	public async Task<EvaluationResult?> Evaluate(IRegisteredFeatureFlag applicationFlag, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		var flagData = await GetEvaluationCriteriaAsync(applicationFlag.Key, cancellationToken);
		if (flagData == null)
		{
			// Auto-create flag in disabled state for deployment scenarios
			await RegisterNewFlagAsync(applicationFlag, cancellationToken);
			return new EvaluationResult
			(
				isEnabled: applicationFlag.DefaultMode == EvaluationMode.Enabled,
				reason: $"A new flag created in the database with specified mode {applicationFlag.DefaultMode}"
			);
		}

		return await _evaluationManager.ProcessEvaluation(flagData, context);
	}

	public async Task<T> GetVariation<T>(IRegisteredFeatureFlag applicationFlag, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			// Get flag once and reuse for both evaluation and variation lookup
			var flagData = await GetEvaluationCriteriaAsync(applicationFlag.Key, cancellationToken);
			if (flagData == null)
			{
				// Auto-create and return default
				await RegisterNewFlagAsync(applicationFlag, cancellationToken);
				return defaultValue;
			}

			// Evaluate using the already-fetched flag
			var result = await _evaluationManager.ProcessEvaluation(flagData, context);
			if (result?.IsEnabled == false)
			{
				return defaultValue;
			}

			// Use the same flag instance for variation lookup
			if (flagData.Variations.Values.TryGetValue(result!.Variation, out var variationValue))
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

	private async Task<EvaluationCriteria?> GetEvaluationCriteriaAsync(string flagKey, CancellationToken cancellationToken)
	{
		EvaluationCriteria? flagData = null;

		// Try cache first
		var cacheKey = new ApplicationCacheKey(flagKey, ApplicationName, ApplicationVersion);
		if (cache != null)
		{
			flagData = await cache.GetAsync(cacheKey, cancellationToken);
		}

		// If not in cache, get from repository
		if (flagData == null)
		{
			flagData = await _repository.GetAsync(new FlagKey(
				key: flagKey,
				scope: Scope.Application,
				applicationName: ApplicationName,
				applicationVersion: ApplicationVersion
			), cancellationToken);
			
			// Cache for future requests if found
			if (flagData != null && cache != null)
			{
				await cache.SetAsync(cacheKey, flagData, cancellationToken);
			}
		}

		return flagData;
	}

	private async Task RegisterNewFlagAsync(IRegisteredFeatureFlag applicationFlag, CancellationToken cancellationToken)
	{
		var flagKey = new FlagKey(applicationFlag.Key, scope: Scope.Application, applicationName: ApplicationName, applicationVersion: ApplicationVersion);
		var defaultFlag = FeatureFlag.Create(
			key: flagKey, 
			name: applicationFlag.Name ?? applicationFlag.Key, 
			description: applicationFlag.Description ?? $"Auto-created flag for {applicationFlag.Key} in application {ApplicationName}");

		if (applicationFlag.DefaultMode == EvaluationMode.Enabled)
		{
			defaultFlag.ActiveEvaluationModes.AddMode(EvaluationMode.Enabled);
		}

		try
		{
			// Save to repository and return the created flag (repository may set additional properties)
			await _repository.CreateAsync(defaultFlag, cancellationToken);
			// Cache for future requests
			if (cache != null)
			{
				// Create composite key for uniqueness per application
				var cacheKey = new ApplicationCacheKey(applicationFlag.Key, ApplicationName, ApplicationVersion);
				await cache.SetAsync(cacheKey, new EvaluationCriteria
				{
					FlagKey = applicationFlag.Key,
					ActiveEvaluationModes = defaultFlag.ActiveEvaluationModes,
				}, cancellationToken);
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