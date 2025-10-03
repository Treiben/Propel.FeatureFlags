using Knara.UtcStrict;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Text.Json;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;

public record FlagSchedule(DateTimeOffset? EnableOnUtc, DateTimeOffset? DisableOnUtc);
public record AuditInfo(DateTimeOffset? TimestampUtc, string? Actor);
public record TimeWindow(TimeOnly? StartOn, TimeOnly? StopOn, string TimeZone = "UTC", DayOfWeek[]? DaysActive = null);

public record FeatureFlagResponse
{
	public string Key { get; set; } 
	public string Name { get; set; } 
	public string Description { get; set; }

	/// <summary>
	/// Off = 0,
	/// On = 1,
	/// Scheduled = 2,
	/// TimeWindow = 3,
	/// UserTargeted = 4,
	/// UserRolloutPercentage = 5,
	/// TenantRolloutPercentage = 6,
	/// TenantTargeted = 7,
	/// TargetingRules = 8,
	/// </summary>
	public EvaluationMode[] Modes { get; set; }

	public AuditInfo Created { get; set; } 
	public AuditInfo? Updated { get; set; }

	public FlagSchedule? Schedule { get; set; }
	public TimeWindow? TimeWindow { get; set; }

	public AccessControl? UserAccess { get; set; }
	public AccessControl? TenantAccess { get; set; }

	public string? TargetingRules { get; set; } 
	public Variations? Variations { get; set; }

	public Dictionary<string, string>? Tags { get; set; } = [];
	public DateTime? ExpirationDate { get; set; }
	public bool IsPermanent { get; set; }

	public string? ApplicationName {get;set; }

	public string? ApplicationVersion { get;set; }
	/// <summary>
	/// Global = 0,
	/// Application = 2,
	/// </summary>
	public Scope Scope { get; set; }

	public FeatureFlagResponse(FeatureFlag flag)
	{
		var identifier = flag.Identifier ?? throw new ArgumentNullException(nameof(flag.Identifier));
		var metadata = flag.Administration ?? throw new ArgumentNullException(nameof(flag.Administration));
		var configuration = flag.EvaluationOptions ?? throw new ArgumentNullException(nameof(flag.EvaluationOptions));
		var retention = metadata.RetentionPolicy ?? throw new ArgumentNullException(nameof(metadata.RetentionPolicy));

		Key = identifier.Key;
		ApplicationName = identifier.ApplicationName;
		ApplicationVersion = identifier.ApplicationVersion;
		Scope = identifier.Scope;

		Name = metadata.Name;
		Description = metadata.Description;

		(Created, Updated) = MapChangeHistory(metadata.ChangeHistory);

		Modes = configuration.ModeSet;

		Schedule = MapSchedule(configuration.Schedule);

		TimeWindow = MapTimeWindow(configuration.OperationalWindow);

		UserAccess = configuration.UserAccessControl;
		TenantAccess = configuration.TenantAccessControl;
		
		TargetingRules = JsonSerializer.Serialize(configuration.TargetingRules, JsonDefaults.JsonOptions);
		Variations = configuration.Variations ?? new Variations();

		Tags = metadata.Tags;
		IsPermanent = retention.IsPermanent;
		ExpirationDate = retention.ExpirationDate;

	}

	private static FlagSchedule? MapSchedule(UtcSchedule schedule)
	{
		if (schedule == null || !schedule.HasSchedule())
		{
			return null;
		}
		return new FlagSchedule(schedule.EnableOn, schedule.DisableOn);
	}

	private static TimeWindow? MapTimeWindow(UtcTimeWindow window)
	{
		if (window == null || !window.HasWindow())
		{
			return null;
		}
		return new TimeWindow(
			TimeOnly.FromTimeSpan(window.StartOn),
			TimeOnly.FromTimeSpan(window.StopOn),
			window.TimeZone,
			window.DaysActive);
	}

	private static (AuditInfo, AuditInfo) MapChangeHistory(List<AuditTrail> changeHistory)
	{
		if (changeHistory == null || changeHistory.Count == 0)
		{
			throw new ArgumentException("Change history cannot be null or empty.", nameof(changeHistory));
		}

		AuditTrail created = changeHistory[0];
		AuditTrail? updated = changeHistory.Count > 1 ? changeHistory[^1] : null;

		var createdInfo = new AuditInfo(created.Timestamp, created.Actor);
		var updatedInfo = updated != null ? new AuditInfo(updated.Timestamp, updated.Actor) : createdInfo;
		return (createdInfo!, updatedInfo!);
	}
}
