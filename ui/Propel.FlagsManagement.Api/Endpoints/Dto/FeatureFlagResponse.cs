using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Propel.FlagsManagement.Api.Endpoints.Dto;

public record FeatureFlagResponse
{
	public string Key { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public FlagEvaluationMode[] EvaluationModes { get; set; } = [];
	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }
	public string CreatedBy { get; set; } = string.Empty;
	public string? UpdatedBy { get; set; } = string.Empty;
	public DateTime? ScheduledEnableDate { get; set; }
	public DateTime? ScheduledDisableDate { get; set; }
	public TimeOnly? WindowStartTime { get; set; }
	public TimeOnly? WindowEndTime { get; set; }
	public string? TimeZone { get; set; }
	public DayOfWeek[]? WindowDays { get; set; }
	public int UserRolloutPercentage { get; set; }
	public List<string> AllowedUsers { get; set; } = [];
	public List<string> BlockedUsers { get; set; } = [];
	public List<TargetingRule> TargetingRules { get; set; } = [];
	public Dictionary<string, object> Variations { get; set; } = [];
	public string DefaultVariation { get; set; } = string.Empty;
	public Dictionary<string, string> Tags { get; set; } = [];
	public DateTime? ExpirationDate { get; set; }
	public bool IsPermanent { get; set; }

	public FeatureFlagResponse() { }

	public FeatureFlagResponse(FeatureFlag flag)
	{
		Key = flag.Key;
		Name = flag.Name;
		Description = flag.Description;
		EvaluationModes = flag.EvaluationModeSet.EvaluationModes;
		CreatedAt = flag.AuditRecord.CreatedAt;
		CreatedBy = flag.AuditRecord.CreatedBy;
		UpdatedAt = flag.AuditRecord.ModifiedAt;
		UpdatedBy = flag.AuditRecord.ModifiedBy;
		IsPermanent = flag.Lifecycle.IsPermanent;
		ExpirationDate = flag.Lifecycle.ExpirationDate;
		ScheduledEnableDate = flag.Schedule.ScheduledEnableDate != FlagActivationSchedule.Unscheduled.ScheduledEnableDate
				? flag.Schedule.ScheduledEnableDate : null;
		ScheduledDisableDate = flag.Schedule.ScheduledDisableDate != FlagActivationSchedule.Unscheduled.ScheduledDisableDate
				? flag.Schedule.ScheduledDisableDate : null;
		WindowStartTime = flag.OperationalWindow.WindowStartTime != FlagOperationalWindow.AlwaysOpen.WindowStartTime 
				? TimeOnly.FromTimeSpan(flag.OperationalWindow.WindowStartTime) : null;
		WindowEndTime = flag.OperationalWindow.WindowEndTime != FlagOperationalWindow.AlwaysOpen.WindowEndTime 
				? TimeOnly.FromTimeSpan(flag.OperationalWindow.WindowEndTime) : null;
		TimeZone = flag.OperationalWindow.TimeZone;
		WindowDays = flag.OperationalWindow.WindowDays;
		UserRolloutPercentage = flag.UserAccess.RolloutPercentage;
		AllowedUsers = flag.UserAccess.AllowedUsers;
		BlockedUsers = flag.UserAccess.BlockedUsers;
		TargetingRules = flag.TargetingRules;
		Variations = flag.Variations.Values;
		DefaultVariation = flag.Variations.DefaultVariation;
		Tags = flag.Tags;
	}
}
