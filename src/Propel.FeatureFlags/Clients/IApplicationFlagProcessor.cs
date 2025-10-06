using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Utilities;
using System.Text.Json;

namespace Propel.FeatureFlags.Clients;

public interface IApplicationFlagProcessor
{
	Task<EvaluationResult?> Evaluate(IFeatureFlag flag, EvaluationContext context, CancellationToken cancellationToken = default);
	Task<T> GetVariation<T>(IFeatureFlag flag, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default);
}

public sealed class ApplicationFlagProcessor(
	IFeatureFlagRepository repository,
	IEvaluatorsSet evaluators,
	IFeatureFlagCache? cache = null) : IApplicationFlagProcessor
{
	private readonly IFeatureFlagRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
	private readonly IEvaluatorsSet _flagProcessor = evaluators ?? throw new ArgumentNullException(nameof(evaluators));

	private string ApplicationName => ApplicationInfo.Name;
	private string ApplicationVersion => ApplicationInfo.Version;

	public async Task<EvaluationResult?> Evaluate(IFeatureFlag applicationFlag, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			var flagConfig = await GetFlagConfiguration(applicationFlag.Key, cancellationToken);
			if (flagConfig == null)
			{
				// Auto-create flag in disabled state for deployment scenarios
				await RegisterApplicationFlag(applicationFlag, cancellationToken);
				return new EvaluationResult
				(
					isEnabled: applicationFlag.OnOffMode == EvaluationMode.On,
					reason: $"An application flag created in the database with specified mode {applicationFlag.OnOffMode} by {ApplicationName} application"
				);
			}

			return await _flagProcessor.Evaluate(flagConfig, context);
		}
		catch (Exception ex)
		{
			if (ex is OperationCanceledException)
				throw;
			// On any error, return disabled result
			return new EvaluationResult(isEnabled: false, reason: $"Error during evaluation: {ex.Message}");
		}
	}

	public async Task<T> GetVariation<T>(IFeatureFlag applicationFlag, T defaultVariation, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		try
		{
			// Get flag once and reuse for both evaluation and variation lookup
			var flagConfig = await GetFlagConfiguration(applicationFlag.Key, cancellationToken);
			if (flagConfig == null)
			{
				// Auto-create and return default
				await RegisterApplicationFlag(applicationFlag, cancellationToken);
				return defaultVariation;
			}

			// Evaluate using the already-fetched flag configuration
			var result = await _flagProcessor.Evaluate(flagConfig, context);
			if (result?.IsEnabled == false)
			{
				return defaultVariation;
			}

			// Variation lookup from the evaluated result
			if (flagConfig.Variations.Values.TryGetValue(result!.Variation, out var variationValue))
			{
				if (variationValue is JsonElement jsonElement)
				{
					var deserializedValue = JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), JsonDefaults.JsonOptions);
					return deserializedValue ?? defaultVariation;
				}

				if (variationValue is T directValue)
				{
					return directValue;
				}

				// Try to convert to requested variation type
				var convertedValue = (T)Convert.ChangeType(variationValue, typeof(T));
				return convertedValue ?? defaultVariation;
			}

			return defaultVariation;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return defaultVariation;
		}
	}

	private async Task<EvaluationOptions?> GetFlagConfiguration(string flagKey, CancellationToken cancellationToken)
	{
		EvaluationOptions? flagData = null;

		// Try cache first
		var cacheKey = new ApplicationCacheKey(flagKey, ApplicationName, ApplicationVersion);
		if (cache != null)
		{
			flagData = await cache.GetAsync(cacheKey, cancellationToken);
		}

		// If not in cache, get from repository
		if (flagData == null)
		{
			flagData = await _repository.GetEvaluationOptionsAsync(new FlagIdentifier(
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

	private async Task RegisterApplicationFlag(IFeatureFlag applicationFlag, CancellationToken cancellationToken)
	{
		var identifier = new FlagIdentifier(applicationFlag.Key, scope: Scope.Application, applicationName: ApplicationName, applicationVersion: ApplicationVersion);
		var activationMode = applicationFlag.OnOffMode == EvaluationMode.On ? EvaluationMode.On : EvaluationMode.Off;
		var name = applicationFlag.Name ?? applicationFlag.Key;
		var description = applicationFlag.Description ?? $"Auto-created flag for {applicationFlag.Key} in application {ApplicationName}";
		try
		{
			// Save to repository and return the created flag (repository may set additional properties)
			await _repository.CreateApplicationFlagAsync(identifier, activationMode, name, description, cancellationToken);
			// Cache for future requests
			if (cache != null)
			{
				// Create composite key for uniqueness per application
				var cacheKey = new ApplicationCacheKey(applicationFlag.Key, ApplicationName, ApplicationVersion);
				await cache.SetAsync(cacheKey,
					new EvaluationOptions(key: identifier.Key, modeSet: new ModeSet([activationMode])),
					cancellationToken);
			}
		}
		catch
		{
			// Even if repository save fails, just return
			// This handles scenarios where repository might be temporarily unavailable
			// The flag will be attempted to be created again on next evaluation
		}
	}
}