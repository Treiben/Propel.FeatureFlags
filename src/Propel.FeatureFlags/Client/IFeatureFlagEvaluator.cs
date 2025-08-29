using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Client.Evaluators;
using Propel.FeatureFlags.Core;
using System.Text.Json;

namespace Propel.FeatureFlags.Client
{
	public interface IFeatureFlagEvaluator
	{
		Task<EvaluationResult> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default);
		Task<T> GetVariation<T>(string flagKey, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default);
	}

	public sealed class FeatureFlagEvaluator : IFeatureFlagEvaluator
	{
		private readonly IFeatureFlagRepository _repository;
		private readonly IFeatureFlagCache? _cache;
		private readonly IFlagEvaluationHandler _evaluationHandler;

		public FeatureFlagEvaluator(
			IFeatureFlagRepository repository,
			IFlagEvaluationHandler evaluationHandler,
			IFeatureFlagCache? cache = null)
		{
			_repository = repository ?? throw new ArgumentNullException(nameof(repository));
			_evaluationHandler = evaluationHandler ?? throw new ArgumentNullException(nameof(evaluationHandler));
			_cache = cache;
		}

		public async Task<EvaluationResult> Evaluate(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default)
		{
			// Try cache first
			FeatureFlag? flag = null;
			if (_cache != null)
				flag = await _cache.GetAsync(flagKey, cancellationToken);

			if (flag == null)
			{
				flag = await _repository.GetAsync(flagKey, cancellationToken);
				if (flag == null)
				{
					// Auto-create flag in disabled state for deployment scenarios
					flag = await CreateDefaultFlagAsync(flagKey, cancellationToken);
					return new EvaluationResult
					(
						isEnabled: false,
						reason: "Flag not found, created default disabled flag"
					);
				}

				// Cache for future requests
				if (_cache != null)
				{
					await _cache.SetAsync(flagKey, flag, TimeSpan.FromMinutes(5), cancellationToken);
				}
			}

			return await _evaluationHandler.Handle(flag, context);
		}

		public async Task<T> GetVariation<T>(string flagKey, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default)
		{
			try
			{
				var result = await Evaluate(flagKey, context, cancellationToken);
				if (!result.IsEnabled)
				{
					return defaultValue;
				}

				FeatureFlag? flag = null;
				if (_cache != null)
				{
					flag = await _cache?.GetAsync(flagKey, cancellationToken);
				}
				if (flag == null)
				{
					flag = await _repository.GetAsync(flagKey, cancellationToken);
				}

				if (flag?.Variations.TryGetValue(result.Variation, out var variationValue) == true)
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

		private async Task<FeatureFlag> CreateDefaultFlagAsync(string flagKey, CancellationToken cancellationToken)
		{
			var now = DateTime.UtcNow;
			var defaultFlag = new FeatureFlag
			{
				Key = flagKey,
				Name = flagKey, // Use key as name initially
				Description = $"Auto-created flag for {flagKey}",
				Status = FeatureFlagStatus.Disabled,
				CreatedAt = now,
				UpdatedAt = now,
				CreatedBy = "System",
				UpdatedBy = "System",
				DefaultVariation = "off",
				Variations = new Dictionary<string, object>
				{
					{ "off", false },
					{ "on", true }
				}
			};

			try
			{
				// Save to repository and return the created flag (repository may set additional properties)
				var createdFlag = await _repository.CreateAsync(defaultFlag, cancellationToken);
				return createdFlag;
			}
			catch (Exception ex)
			{
				// Even if repository save fails, return the default flag for this evaluation
				// This handles scenarios where repository might be temporarily unavailable
				// The flag will be attempted to be created again on next evaluation
				return defaultFlag;
			}
		}
	}
}