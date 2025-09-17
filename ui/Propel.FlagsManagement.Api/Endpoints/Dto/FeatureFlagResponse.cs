using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using System.Text.Json;

namespace Propel.FlagsManagement.Api.Endpoints.Dto;

public record FlagSchedule(DateTimeOffset? EnableOnUtc, DateTimeOffset? DisableOnUtc);
public record AuditInfo(DateTimeOffset? TimestampUtc, string? Actor);
public record TimeWindow(TimeOnly? StartOn, TimeOnly? StopOn, string TimeZone = "UTC", DayOfWeek[]? DaysActive = null);

public record FeatureFlagResponse
{
	public string Key { get; set; } 
	public string Name { get; set; } 
	public string Description { get; set; } 

	public EvaluationMode[] Modes { get; set; }

	public AuditInfo Created { get; set; } 
	public AuditInfo? Updated { get; set; }

	public FlagSchedule? Schedule { get; set; }
	public TimeWindow? TimeWindow { get; set; }

	public AccessControl? UserAccess { get; set; }
	public AccessControl? TenantAccess { get; set; }

	public string? TargetingRules { get; set; } 
	public Variations Variations { get; set; }

	public Dictionary<string, string>? Tags { get; set; } = [];
	public DateTime? ExpirationDate { get; set; }
	public bool IsPermanent { get; set; }

	public string? ApplicationName {get;set; }

	public string? ApplicationVersion { get;set; }
	public Scope Scope { get; set; }

	public FeatureFlagResponse(FeatureFlag flag)
	{
		Key = flag.Key.Key;
		Name = flag.Name;
		Description = flag.Description;

		Modes = [.. flag.ActiveEvaluationModes.Modes];

		Created = MapAudit(flag.Created)!;
		Updated = MapAudit(flag.LastModified);

		Schedule = MapSchedule(flag.Schedule);

		TimeWindow = MapTimeWindow(flag.OperationalWindow);

		UserAccess = flag.UserAccessControl;
		TenantAccess = flag.TenantAccessControl;
		
		TargetingRules = JsonSerializer.Serialize(flag.TargetingRules, JsonDefaults.JsonOptions);
		Variations = flag.Variations;

		Tags = flag.Tags;
		IsPermanent = flag.Retention.IsPermanent;
		ExpirationDate = flag.Retention.ExpirationDate;

		ApplicationName = flag.Key.ApplicationName;
		ApplicationVersion = flag.Key.ApplicationVersion;
		Scope = flag.Key.Scope;
	}

	private static FlagSchedule? MapSchedule(ActivationSchedule schedule)
	{
		if (schedule == null || !schedule.HasSchedule())
		{
			return null;
		}
		return new FlagSchedule(new DateTimeOffset(schedule.EnableOn), new DateTimeOffset(schedule.DisableOn));
	}

	private static TimeWindow? MapTimeWindow(OperationalWindow window)
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

	private static AuditInfo? MapAudit(AuditTrail? audit)
	{
		if (audit == null)
		{
			return null;
		}
		return new AuditInfo(new DateTimeOffset(audit.Timestamp), audit.Actor);
	}
}
