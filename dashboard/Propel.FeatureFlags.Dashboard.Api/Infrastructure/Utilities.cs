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
			metadata.Retention = new RetentionPolicy(metadataEntity.IsPermanent, metadataEntity.ExpirationDate.DateTime);
		}

		return metadata;
	}

	public static FlagEvaluationConfiguration MapConfigurationToDomain(FlagIdentifier identifier, Entities.FeatureFlag entity)
	{
		// Parse evaluation modes
		var evaluationModes = Parser.ParseEvaluationModes(entity.EvaluationModes);

		// Parse schedule
		var schedule = entity.ScheduledEnableDate.HasValue || entity.ScheduledDisableDate.HasValue
			? ActivationSchedule.CreateSchedule(entity.ScheduledEnableDate?.DateTime, entity.ScheduledDisableDate?.DateTime)
			: ActivationSchedule.Unscheduled;

		// Parse operational window
		var operationalWindow = entity.WindowStartTime.HasValue || entity.WindowEndTime.HasValue
			? new OperationalWindow(
				entity.WindowStartTime?.ToTimeSpan() ?? TimeSpan.Zero,
				entity.WindowEndTime?.ToTimeSpan() ?? TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59)),
				entity.TimeZone ?? "UTC",
				Parser.ParseWindowDays(entity.WindowDays))
			: OperationalWindow.AlwaysOpen;

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

public static class Filtering
{
	public static IQueryable<Entities.FeatureFlag> ApplyFilters(IQueryable<Entities.FeatureFlag> query, FeatureFlagFilter filter)
	{
		// Filter by application scope
		if (!string.IsNullOrEmpty(filter.ApplicationName))
		{
			query = query.Where(f => f.ApplicationName == filter.ApplicationName);
		}

		if (!string.IsNullOrEmpty(filter.ApplicationVersion))
		{
			query = query.Where(f => f.ApplicationVersion == filter.ApplicationVersion);
		}

		if (filter.Scope.HasValue)
		{
			query = query.Where(f => f.Scope == (int)filter.Scope.Value);
		}

		// Filter by evaluation modes
		if (filter.EvaluationModes?.Length > 0)
		{
			query = query.Where(f => FilterByEvaluationModes(f.EvaluationModes, filter.EvaluationModes));
		}

		return query;
	}

	public static bool FilterByEvaluationModes(string evaluationModesJson, EvaluationMode[] filterModes)
	{
		try
		{
			var modes = JsonSerializer.Deserialize<int[]>(evaluationModesJson) ?? [];
			return filterModes.Any(filterMode => modes.Contains((int)filterMode));
		}
		catch
		{
			return false;
		}
	}
}
