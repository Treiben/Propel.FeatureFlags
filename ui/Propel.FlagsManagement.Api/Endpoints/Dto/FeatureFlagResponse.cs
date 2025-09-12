using Propel.FeatureFlags.Core;
using System.Text.Json;

namespace Propel.FlagsManagement.Api.Endpoints.Dto;

public record ActivationSchedule(DateTimeOffset? EnableOnUtc, DateTimeOffset? DisableOnUtc);
public record Audit(DateTimeOffset? TimestampUtc, string? Actor);
public record OperationalWindow(TimeOnly? StartOn, TimeOnly? StopOn, string TimeZone = "UTC", DayOfWeek[]? DaysActive = null);

public record FeatureFlagResponse
{
	public string Key { get; set; } 
	public string Name { get; set; } 
	public string Description { get; set; } 

	public EvaluationMode[] Modes { get; set; }

	public Audit Created { get; set; } 
	public Audit Updated { get; set; }

	public ActivationSchedule? Schedule { get; set; }
	public OperationalWindow? TimeWindow { get; set; }

	public AccessControl? UserAccess { get; set; }
	public AccessControl? TenantAccess { get; set; }

	public string? TargetingRules { get; set; } 

	public Variations Variations { get; set; }

	public Dictionary<string, string>? Tags { get; set; } = [];
	public DateTime? ExpirationDate { get; set; }
	public bool IsPermanent { get; set; }

	public FeatureFlagResponse(FeatureFlag flag)
	{
		Key = flag.Key;
		Name = flag.Name;
		Description = flag.Description;

		Modes = flag.ActiveEvaluationModes.Modes;

		Created = new Audit(new DateTimeOffset(flag?.Created.Timestamp ?? DateTime.MinValue), flag?.Created.Actor);
		Updated = new Audit(new DateTimeOffset(flag?.LastModified?.Timestamp ?? flag.Created.Timestamp), flag?.LastModified?.Actor ?? flag.Created.Actor);
		
		Schedule = flag.Schedule?.HasSchedule() == true 
			? new ActivationSchedule(
				new DateTimeOffset(flag.Schedule.EnableOn), 
				flag.Schedule.DisableOn.HasValue ? new DateTimeOffset(flag.Schedule.DisableOn.Value) : null)
			: null;

		TimeWindow = flag.OperationalWindow?.HasWindow() == true 
			? new OperationalWindow(
				TimeOnly.FromTimeSpan(flag.OperationalWindow.StartOn), 
				TimeOnly.FromTimeSpan(flag.OperationalWindow.StopOn),
				flag.OperationalWindow.TimeZone,
				flag.OperationalWindow.DaysActive)
			: null;

		UserAccess = flag.UserAccessControl;
		TenantAccess = flag.TenantAccessControl;
		
		TargetingRules = JsonSerializer.Serialize(flag.TargetingRules, JsonDefaults.JsonOptions);

		Variations = flag.Variations;

		Tags = flag.Tags;
		IsPermanent = flag.Lifecycle.IsPermanent;
		ExpirationDate = flag.Lifecycle.ExpirationDate;
	}
}
