using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using System.Text.Json;

namespace Propel.FeatureFlags.Evaluation;

public interface IFeatureFlagEvaluator
{
	Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default);
	Task<T> GetVariation<T>(string flagKey, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default);
}

public sealed class FeatureFlagEvaluator(
	IFeatureFlagRepository repository,
	IFlagEvaluationManager evaluationManager,
	IFeatureFlagCache? cache = null) : IFeatureFlagEvaluator
{
	private readonly IFeatureFlagRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IFlagEvaluationManager _evaluationManager = evaluationManager ?? throw new ArgumentNullException(nameof(evaluationManager));

	public async Task<EvaluationResult?> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		var flag = await GetFlagAsync(flagKey, cancellationToken);
		if (flag == null)
		{
			// Auto-create flag in disabled state for deployment scenarios
			await CreateDefaultFlagAsync(flagKey, cancellationToken);
			return new EvaluationResult
			(
				isEnabled: false,
				reason: "Flag not found, created default disabled flag"
			);
		}

		return await _evaluationManager.ProcessEvaluation(flag, context);
	}

	public async Task<T> GetVariation<T>(string flagKey, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			// Get flag once and reuse for both evaluation and variation lookup
			var flag = await GetFlagAsync(flagKey, cancellationToken);
			if (flag == null)
			{
				// Auto-create and return default
				await CreateDefaultFlagAsync(flagKey, cancellationToken);
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
		// Try cache first
		FeatureFlag? flag = null;
		if (cache != null)
		{
			flag = await cache.GetAsync(flagKey, cancellationToken);
		}

		// If not in cache, get from repository
		if (flag == null)
		{
			flag = await _repository.GetAsync(flagKey, cancellationToken);
			
			// Cache for future requests if found
			if (flag != null && cache != null)
			{
				await cache.SetAsync(flagKey, flag, TimeSpan.FromMinutes(5), cancellationToken);
			}
		}

		return flag;
	}

	private async Task CreateDefaultFlagAsync(string flagKey, CancellationToken cancellationToken)
	{
		var defaultFlag = new FeatureFlag
		{
			Key = flagKey,
			Name = flagKey, // Use key as name initially
			Description = $"Auto-created flag for {flagKey}"
		};

		try
		{
			// Save to repository and return the created flag (repository may set additional properties)
			var createdFlag = await _repository.CreateAsync(defaultFlag, cancellationToken);
			// Cache for future requests
			if (cache != null)
			{
				await cache.SetAsync(flagKey, createdFlag, TimeSpan.FromMinutes(5), cancellationToken);
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