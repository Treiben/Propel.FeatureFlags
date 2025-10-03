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
		var metadata = MapMetadataToDomain(entity);

		// Create configuration
		var configuration = MapConfigurationToDomain(entity);

		return new FeatureFlag(identifier, metadata, configuration);
	}

	public static FlagAdministration MapMetadataToDomain(Entities.FeatureFlag entity) => new(
			Name: entity.Name,
			Description: entity.Description ?? string.Empty,
			RetentionPolicy: new RetentionPolicy(entity.Metadata.ExpirationDate),
			Tags: JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Metadata.Tags) ?? [],
			ChangeHistory: [.. entity.AuditTrail
				.OrderByDescending(t => t.Timestamp)
				.Select(MapAuditTrailToDomain)]);

	public static AuditTrail MapAuditTrailToDomain(Entities.FeatureFlagAudit entity) => new(
				Timestamp: entity.Timestamp,
				Actor: entity.Actor,
				Action: entity.Action,
				Notes: entity.Notes
			);

	public static FlagEvaluationOptions MapConfigurationToDomain(Entities.FeatureFlag entity)
	{
		// Parse evaluation modes
		ModeSet evaluationModes = Parser.ParseEvaluationModes(entity.EvaluationModes);

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

		return new FlagEvaluationOptions(
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
	public static ModeSet ParseEvaluationModes(string json)
	{
		try
		{
			var modes = JsonSerializer.Deserialize<int[]>(json, JsonDefaults.JsonOptions) ?? [];
			var enumModes = modes.Select(m => (EvaluationMode)m).ToHashSet();
			return new ModeSet(enumModes);
		}
		catch
		{
			return EvaluationMode.Off;
		}
	}

	public static DayOfWeek[] ParseWindowDays(string json)
	{
		try
		{
			var days = JsonSerializer.Deserialize<int[]>(json, JsonDefaults.JsonOptions) ?? [];
			return [.. days.Select(d => Enum.IsDefined<DayOfWeek>((DayOfWeek)d) ? (DayOfWeek)d : throw new ArgumentException())];
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
			return new Variations();
		}
	}
}

