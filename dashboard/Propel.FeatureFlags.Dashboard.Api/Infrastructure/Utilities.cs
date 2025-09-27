using Knara.UtcStrict;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public static class Mapper
{
	public static FeatureFlag MapToDomain(Entities.FeatureFlag entity)
	{
		// Create identifier
		var identifier = new FlagIdentifier(
			entity.Key,
			(Scope)entity.Scope,
			entity.ApplicationName,
			entity.ApplicationVersion);

		// Create metadata
		var metadata = MapMetadataToDomain(identifier, entity);

		// Create configuration
		var configuration = MapConfigurationToDomain(identifier, entity);

		return new FeatureFlag(identifier, metadata, configuration);
	}

	public static Metadata MapMetadataToDomain(FlagIdentifier identifier, Entities.FeatureFlag entity)
	{
		var metadata = Metadata.Create(identifier, entity.Name, entity.Description);

		var metadataEntity = entity.Metadata;
		if (metadataEntity != null)
		{
			// Parse tags
			try
			{
				var tags = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataEntity.Tags) ?? [];
				metadata.Tags = tags;
			}
			catch
			{
				metadata.Tags = [];
			}

			// Set retention policy
			metadata.RetentionPolicy = new RetentionPolicy(metadataEntity.ExpirationDate);
			metadata.Created = MapAuditTrailToDomain(entity.AuditTrail.First(t => t.Action == "flag-created"));
			metadata.LastModified = MapAuditTrailToDomain(entity.AuditTrail.OrderByDescending(t => t.Timestamp).First());
		}

		return metadata;
	}

	public static AuditTrail MapAuditTrailToDomain(Entities.FeatureFlagAudit entity)
	{
		return new AuditTrail(
				timestamp: entity.Timestamp,
				actor: entity.Actor,
				action: entity.Action,
				reason: entity.Reason
			);
	}

	public static FlagEvaluationConfiguration MapConfigurationToDomain(FlagIdentifier identifier, Entities.FeatureFlag entity)
	{
		// Parse evaluation modes
		var evaluationModes = Parser.ParseEvaluationModes(entity.EvaluationModes);

		// Parse schedule
		var schedule =new  UtcSchedule(entity.ScheduledEnableDate ?? UtcDateTime.MinValue,
													entity.ScheduledDisableDate ?? UtcDateTime.MaxValue);

		// Parse operational window
		var alwaysopen = UtcTimeWindow.AlwaysOpen;
		var operationalWindow = new UtcTimeWindow(
				entity.WindowStartTime?.ToTimeSpan() ?? alwaysopen.StartOn,
				entity.WindowEndTime?.ToTimeSpan() ?? alwaysopen.StopOn,
				entity.TimeZone ?? "UTC",
				Parser.ParseWindowDays(entity.WindowDays));

		// Parse targeting rules
		var targetingRules = Parser.ParseTargetingRules(entity.TargetingRules);

		// Parse user access control
		var userAccessControl = new AccessControl(
			Parser.ParseStringList(entity.EnabledUsers),
			Parser.ParseStringList(entity.DisabledUsers),
			entity.UserPercentageEnabled);

		// Parse tenant access control
		var tenantAccessControl = new AccessControl(
			Parser.ParseStringList(entity.EnabledTenants),
			Parser.ParseStringList(entity.DisabledTenants),
			entity.TenantPercentageEnabled);

		// Parse variations
		var variations = Parser.ParseVariations(entity.Variations, entity.DefaultVariation);

		return new FlagEvaluationConfiguration(
			identifier,
			evaluationModes,
			schedule,
			operationalWindow,
			targetingRules,
			userAccessControl,
			tenantAccessControl,
			variations);
	}
}

public static class Parser
{
	public static EvaluationModes ParseEvaluationModes(string json)
	{
		try
		{
			var modes = JsonSerializer.Deserialize<int[]>(json, JsonDefaults.JsonOptions) ?? [];
			var enumModes = modes.Select(m => (EvaluationMode)m).ToHashSet();
			return new EvaluationModes(enumModes);
		}
		catch
		{
			return EvaluationModes.FlagIsDisabled;
		}
	}

	public static DayOfWeek[] ParseWindowDays(string json)
	{
		try
		{
			var days = JsonSerializer.Deserialize<int[]>(json, JsonDefaults.JsonOptions) ?? [];
			return [.. days.Select(d => (DayOfWeek)d)];
		}
		catch
		{
			return [];
		}
	}

	public static List<ITargetingRule> ParseTargetingRules(string json)
	{
		try
		{
			// This would need a more sophisticated targeting rule parser
			// For now, return empty list
			return JsonSerializer.Deserialize<List<ITargetingRule>>(json, JsonDefaults.JsonOptions) ?? [];
		}
		catch
		{
			return [];
		}
	}

	public static List<string> ParseStringList(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<List<string>>(json, JsonDefaults.JsonOptions) ?? [];
		}
		catch
		{
			return [];
		}
	}

	public static Variations ParseVariations(string json, string defaultVariation)
	{
		try
		{
			var values = JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonDefaults.JsonOptions) ?? [];
			var variations = new Variations
			{
				Values = values,
				DefaultVariation = defaultVariation
			};
			return variations;
		}
		catch
		{
			return Variations.OnOff;
		}
	}
}

