using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Persistence;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Propel.FeatureFlags.Client;

public record EvaluationResult
{
	public EvaluationResult(bool isEnabled, string? variation = default, string? reason = default, Dictionary<string, object>? metadata = default)
	{
		IsEnabled = isEnabled;
		Variation = variation ?? "off";
		Reason = reason ?? "";
		Metadata = metadata ?? [];
	}

	public bool IsEnabled { get; }
	public string Variation { get; } 
	public string Reason { get; }
	public Dictionary<string, object> Metadata { get; }
}

public record EvaluationContext
{
	public EvaluationContext(string? userId, Dictionary<string, object>? attributes = default, DateTime? evaluationTime = default, string? timeZone = "UTC")
	{
		UserId = userId;
		Attributes = attributes ?? [];
		EvaluationTime = evaluationTime ?? DateTime.UtcNow;
		TimeZone = timeZone;
	}

	public string? UserId { get; }
	public Dictionary<string, object> Attributes { get; } 
	public DateTime? EvaluationTime { get; }
	public string? TimeZone { get; }
}

public interface IFeatureFlagEvaluator
{
	Task<EvaluationResult> EvaluateAsync(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default);
	Task<T> GetVariationAsync<T>(string flagKey, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default);

	//Task<Dictionary<string, bool>> IsEnabledBulkAsync(IEnumerable<string> flagKeys, string? userId = null, Dictionary<string, object>? attributes = null);
}

public sealed class FeatureFlagEvaluator(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<FeatureFlagEvaluator> logger) : IFeatureFlagEvaluator
{
	public async Task<EvaluationResult> EvaluateAsync(string flagKey, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		logger.LogDebug("Starting evaluation for flag {FlagKey} with userId {UserId}", flagKey, context.UserId);

		// Try cache first
		var flag = await cache.GetAsync(flagKey, cancellationToken);
		if (flag == null)
		{
			logger.LogDebug("Flag {FlagKey} not found in cache, checking repository", flagKey);
			
			// Fallback to repository
			flag = await repository.GetAsync(flagKey, cancellationToken);
			if (flag == null)
			{
				logger.LogDebug("Flag {FlagKey} not found in repository, returning disabled", flagKey);
				return new EvaluationResult(isEnabled: false, variation: "off", reason: "Flag not found", metadata: []);;
			}

			logger.LogDebug("Flag {FlagKey} loaded from repository with status {Status}", flagKey, flag.Status);

			// Cache for future requests
			await cache.SetAsync(flagKey, flag, TimeSpan.FromMinutes(5), cancellationToken);
			logger.LogDebug("Flag {FlagKey} cached for 5 minutes", flagKey);
		}
		else
		{
			logger.LogDebug("Flag {FlagKey} found in cache with status {Status}", flagKey, flag.Status);
		}

		var result = await EvaluateFlagAsync(flag, context);
		logger.LogDebug("Flag {FlagKey} evaluation completed: IsEnabled={IsEnabled}, Variation={Variation}, Reason={Reason}", 
			flagKey, result.IsEnabled, result.Variation, result.Reason);

		return result;
	}

	public async Task<T> GetVariationAsync<T>(string flagKey, T defaultValue, EvaluationContext context, CancellationToken cancellationToken = default)
	{
		logger.LogDebug("Getting variation for flag {FlagKey} with default value type {DefaultType}", flagKey, typeof(T).Name);

		try
		{
			var result = await EvaluateAsync(flagKey, context, cancellationToken);
			if (!result.IsEnabled)
			{
				logger.LogDebug("Flag {FlagKey} is disabled, returning default value", flagKey);
				return defaultValue;
			}

			var flag = await cache.GetAsync(flagKey, cancellationToken) ??
					  await repository.GetAsync(flagKey, cancellationToken);

			if (flag?.Variations.TryGetValue(result.Variation, out var variationValue) == true)
			{
				logger.LogDebug("Found variation {Variation} for flag {FlagKey}", result.Variation, flagKey);

				if (variationValue is JsonElement jsonElement)
				{
					var deserializedValue = JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
					logger.LogDebug("Deserialized JSON variation for flag {FlagKey}", flagKey);
					return deserializedValue ?? defaultValue;
				}

				if (variationValue is T directValue)
				{
					logger.LogDebug("Using direct variation value for flag {FlagKey}", flagKey);
					return directValue;
				}

				// Try to convert
				var convertedValue = (T)Convert.ChangeType(variationValue, typeof(T));
				logger.LogDebug("Converted variation value for flag {FlagKey} from {FromType} to {ToType}", 
					flagKey, variationValue.GetType().Name, typeof(T).Name);
				return convertedValue ?? defaultValue;
			}

			logger.LogDebug("No variation {Variation} found for flag {FlagKey}, returning default value", result.Variation, flagKey);
			return defaultValue;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogError(ex, "Error evaluating variation for flag {FlagKey}", flagKey);
			return defaultValue;
		}
	}

	private async Task<EvaluationResult> EvaluateFlagAsync(FeatureFlag flag, EvaluationContext context)
	{
		logger.LogDebug("Evaluating flag {FlagKey} with status {Status}", flag.Key, flag.Status);

		var evaluationTime = context.EvaluationTime ?? DateTime.UtcNow;
		var userTimeZone = !string.IsNullOrEmpty(context.TimeZone) ? context.TimeZone : flag.TimeZone ?? "UTC";

		logger.LogDebug("Using evaluation time {EvaluationTime} and timezone {TimeZone} for flag {FlagKey}", 
			evaluationTime, userTimeZone, flag.Key);

		// Check if flag is expired
		if (flag.ExpirationDate.HasValue && flag.ExpirationDate.Value != default && evaluationTime > flag.ExpirationDate.Value)
		{
			logger.LogDebug("Flag {FlagKey} expired at {ExpirationDate}, current time {EvaluationTime}", 
				flag.Key, flag.ExpirationDate.Value, evaluationTime);
			return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Flag expired");
		}

		// Check explicit user overrides first
		if (!string.IsNullOrEmpty(context.UserId))
		{
			if (flag.DisabledUsers.Contains(context.UserId))
			{
				logger.LogDebug("User {UserId} is explicitly disabled for flag {FlagKey}", context.UserId, flag.Key);
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "User explicitly disabled");
			}

			if (flag.EnabledUsers.Contains(context.UserId))
			{
				logger.LogDebug("User {UserId} is explicitly enabled for flag {FlagKey}", context.UserId, flag.Key);
				return new EvaluationResult(isEnabled: true, variation: "on", reason: "User explicitly enabled");
			}
		}

		// Evaluate based on flag status
		switch (flag.Status)
		{
			case FeatureFlagStatus.Disabled:
				logger.LogDebug("Flag {FlagKey} is globally disabled", flag.Key);
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Flag disabled");

			case FeatureFlagStatus.Enabled:
				logger.LogDebug("Flag {FlagKey} is globally enabled", flag.Key);
				return new EvaluationResult(isEnabled: true, variation: "on", reason: "Flag enabled");

			case FeatureFlagStatus.Scheduled:
				logger.LogDebug("Evaluating scheduled flag {FlagKey}", flag.Key);
				return await EvaluateScheduledFlag(flag, evaluationTime);

			case FeatureFlagStatus.TimeWindow:
				logger.LogDebug("Evaluating time window flag {FlagKey}", flag.Key);
				return await EvaluateTimeWindowFlag(flag, evaluationTime, userTimeZone);

			case FeatureFlagStatus.UserTargeted:
				logger.LogDebug("Evaluating user targeted flag {FlagKey}", flag.Key);
				return await EvaluateTargetedFlag(flag, context);

			case FeatureFlagStatus.Percentage:
				logger.LogDebug("Evaluating percentage rollout flag {FlagKey}", flag.Key);
				return await EvaluatePercentageFlag(flag, context);

			default:
				logger.LogWarning("Unknown flag status {Status} for flag {FlagKey}", flag.Status, flag.Key);
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Unknown flag status");
		}
	}

	private async Task<EvaluationResult> EvaluateScheduledFlag(FeatureFlag flag, DateTime evaluationTime)
	{
		logger.LogDebug("Evaluating scheduled flag {FlagKey}: EnableDate={EnableDate}, DisableDate={DisableDate}, CurrentTime={CurrentTime}", 
			flag.Key, flag.ScheduledEnableDate, flag.ScheduledDisableDate, evaluationTime);

		// Check if we're in the scheduled enable period
		if (flag.ScheduledEnableDate.HasValue && evaluationTime >= flag.ScheduledEnableDate.Value)
		{
			// Check if we've passed the disable date
			if (flag.ScheduledDisableDate.HasValue && evaluationTime >= flag.ScheduledDisableDate.Value)
			{
				logger.LogDebug("Flag {FlagKey} past scheduled disable date", flag.Key);
				return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Scheduled disable date passed");
			}

			logger.LogDebug("Flag {FlagKey} within scheduled enable period", flag.Key);
			return new EvaluationResult(isEnabled: true, variation: "on", reason: "Scheduled enable date reached");
		}

		logger.LogDebug("Flag {FlagKey} before scheduled enable date", flag.Key);
		return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Scheduled enable date not reached");
	}

	private async Task<EvaluationResult> EvaluateTimeWindowFlag(FeatureFlag flag, DateTime evaluationTime, string timeZone)
	{
		if (!flag.WindowStartTime.HasValue || !flag.WindowEndTime.HasValue)
		{
			logger.LogDebug("Flag {FlagKey} time window not properly configured", flag.Key);
			return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Time window not configured");
		}

		// Convert to specified timezone
		var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
		var localTime = TimeZoneInfo.ConvertTimeFromUtc(evaluationTime, timeZoneInfo);
		var currentTime = localTime.TimeOfDay; // TimeOnly.FromDateTime(localTime);
		var currentDay = localTime.DayOfWeek;

		logger.LogDebug("Flag {FlagKey} time window evaluation: LocalTime={LocalTime}, CurrentDay={CurrentDay}, WindowStart={WindowStart}, WindowEnd={WindowEnd}", 
			flag.Key, localTime, currentDay, flag.WindowStartTime.Value, flag.WindowEndTime.Value);

		// Check if current day is in allowed days
		if (flag.WindowDays?.Any() == true && !flag.WindowDays.Contains(currentDay))
		{
			logger.LogDebug("Flag {FlagKey} current day {CurrentDay} not in allowed days {AllowedDays}", 
				flag.Key, currentDay, string.Join(", ", flag.WindowDays));
			return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "Outside allowed days");
		}

		// Check time window
		bool inWindow;
		if (flag.WindowStartTime.Value <= flag.WindowEndTime.Value)
		{
			// Same day window (e.g., 9:00 - 17:00)
			inWindow = currentTime >= flag.WindowStartTime.Value && currentTime <= flag.WindowEndTime.Value;
			logger.LogDebug("Flag {FlagKey} same-day window check: {CurrentTime} between {StartTime} and {EndTime} = {InWindow}", 
				flag.Key, currentTime, flag.WindowStartTime.Value, flag.WindowEndTime.Value, inWindow);
		}
		else
		{
			// Overnight window (e.g., 22:00 - 06:00)
			inWindow = currentTime >= flag.WindowStartTime.Value || currentTime <= flag.WindowEndTime.Value;
			logger.LogDebug("Flag {FlagKey} overnight window check: {CurrentTime} after {StartTime} or before {EndTime} = {InWindow}", 
				flag.Key, currentTime, flag.WindowStartTime.Value, flag.WindowEndTime.Value, inWindow);
		}

		return new EvaluationResult(isEnabled: inWindow, variation: inWindow ? "on" : flag.DefaultVariation, reason: inWindow ? "Within time window" : "Outside time window");
	}

	private async Task<EvaluationResult> EvaluateTargetedFlag(FeatureFlag flag, EvaluationContext context)
	{
		logger.LogDebug("Evaluating {RuleCount} targeting rules for flag {FlagKey}", flag.TargetingRules.Count, flag.Key);

		// Evaluate targeting rules
		foreach (var rule in flag.TargetingRules)
		{
			logger.LogDebug("Evaluating targeting rule for flag {FlagKey}: {Attribute} {Operator} [{Values}]", 
				flag.Key, rule.Attribute, rule.Operator, string.Join(", ", rule.Values));

			if (EvaluateTargetingRule(rule, context))
			{
				logger.LogDebug("Targeting rule matched for flag {FlagKey}: {Attribute} {Operator} [{Values}] -> {Variation}", 
					flag.Key, rule.Attribute, rule.Operator, string.Join(", ", rule.Values), rule.Variation);
				return new EvaluationResult(isEnabled: true, variation: rule.Variation, reason: $"Targeting rule matched: {rule.Attribute} {rule.Operator} {string.Join(",", rule.Values)}");
			}
			else
			{
				logger.LogDebug("Targeting rule did not match for flag {FlagKey}: {Attribute} {Operator} [{Values}]", 
					flag.Key, rule.Attribute, rule.Operator, string.Join(", ", rule.Values));
			}
		}

		logger.LogDebug("No targeting rules matched for flag {FlagKey}", flag.Key);
		return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "No targeting rules matched");
	}

	private async Task<EvaluationResult> EvaluatePercentageFlag(FeatureFlag flag, EvaluationContext context)
	{
		if (string.IsNullOrEmpty(context.UserId))
		{
			logger.LogDebug("Flag {FlagKey} requires user ID for percentage rollout but none provided", flag.Key);
			return new EvaluationResult(isEnabled: false, variation: flag.DefaultVariation, reason: "User ID required for percentage rollout");
		}

		// Use consistent hashing to ensure same user always gets same result
		var hashInput = $"{flag.Key}:{context.UserId}";
		var hash = ComputeHash(hashInput);
		var percentage = hash % 100;

		logger.LogDebug("Flag {FlagKey} percentage calculation: Hash({HashInput}) = {Hash}, Percentage = {Percentage}, Threshold = {Threshold}", 
			flag.Key, hashInput, hash, percentage, flag.PercentageEnabled);

		var isEnabled = percentage < flag.PercentageEnabled;
		logger.LogDebug("Flag {FlagKey} percentage rollout result: {Percentage}% < {Threshold}% = {IsEnabled}", 
			flag.Key, percentage, flag.PercentageEnabled, isEnabled);

		return new EvaluationResult(isEnabled: isEnabled, 
			variation: isEnabled ? "on" : flag.DefaultVariation, 
			reason: $"Percentage rollout: {percentage}% < {flag.PercentageEnabled}%");
	}

	private bool EvaluateTargetingRule(TargetingRule rule, EvaluationContext context)
	{
		if (!context.Attributes.TryGetValue(rule.Attribute, out var attributeValue))
		{
			logger.LogDebug("Attribute {Attribute} not found in evaluation context", rule.Attribute);
			return false;
		}

		var stringValue = attributeValue?.ToString() ?? string.Empty;
		logger.LogDebug("Evaluating targeting rule: {Attribute}='{Value}' {Operator} [{RuleValues}]", 
			rule.Attribute, stringValue, rule.Operator, string.Join(", ", rule.Values));

		var result = rule.Operator switch
		{
			TargetingOperator.Equals => rule.Values.Contains(stringValue, StringComparer.OrdinalIgnoreCase),
			TargetingOperator.NotEquals => !rule.Values.Contains(stringValue, StringComparer.OrdinalIgnoreCase),
			TargetingOperator.Contains => rule.Values.Any(v => stringValue.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0),
			TargetingOperator.NotContains => rule.Values.All(v => stringValue.IndexOf(v, StringComparison.OrdinalIgnoreCase) < 0),
			TargetingOperator.In => rule.Values.Contains(stringValue, StringComparer.OrdinalIgnoreCase),
			TargetingOperator.NotIn => !rule.Values.Contains(stringValue, StringComparer.OrdinalIgnoreCase),
			TargetingOperator.GreaterThan => double.TryParse(stringValue, out var numValue) &&
										   rule.Values.Any(v => double.TryParse(v, out var ruleValue) && numValue > ruleValue),
			TargetingOperator.LessThan => double.TryParse(stringValue, out var numValue2) &&
										rule.Values.Any(v => double.TryParse(v, out var ruleValue) && numValue2 < ruleValue),
			_ => false
		};

		logger.LogDebug("Targeting rule evaluation result: {Result}", result);
		return result;
	}

	private static uint ComputeHash(string input)
	{
		using var sha256 = SHA256.Create();
		var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
		return BitConverter.ToUInt32(hash, 0);
	}
}