using Propel.FeatureFlags.Core;
using System.Text.Json;

namespace Propel.FlagsManagement.Api.Endpoints.Dto;

public record FeatureFlagResponse
{
	public string Key { get; set; } 
	public string Name { get; set; } 
	public string Description { get; set; } 

	public EvaluationMode[] Modes { get; set; }

	public Audit Created { get; set; } 
	public Audit Updated { get; set; }

	public ActivationSchedule Schedule { get; set; }
	public OperationalWindow OperationalWindow { get; set; }

	public AccessControl UserAccess { get; set; }
	public AccessControl TenantAccess { get; set; }

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

		Created = flag.Created;
		Updated = flag.LastModified ?? flag.Created;
		
		Schedule = flag.Schedule;
		OperationalWindow = flag.OperationalWindow;

		UserAccess = flag.UserAccessControl;
		TenantAccess = flag.TenantAccessControl;
		
		TargetingRules = JsonSerializer.Serialize(flag.TargetingRules, JsonDefaults.JsonOptions);

		Variations = flag.Variations;

		Tags = flag.Tags;
		IsPermanent = flag.Lifecycle.IsPermanent;
		ExpirationDate = flag.Lifecycle.ExpirationDate;
	}
}
