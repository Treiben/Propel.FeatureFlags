using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public record Metadata(
	string Name,
	string Description,
	RetentionPolicy RetentionPolicy,
	Dictionary<string, string> Tags,
	List<AuditTrail> ChangeHistory)
{
	public static Metadata Create(Scope scope, string name, string description, AuditTrail initial)
	{
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(initial);

		description = description ?? string.Empty;
		var retentionPolicy = scope == Scope.Global ? RetentionPolicy.GlobalPolicy : RetentionPolicy.OneMonthRetentionPolicy;
		var tags = new Dictionary<string, string>();
		var changeHistory = new List<AuditTrail> { initial };

		return new Metadata(
			Name: name.Trim(),
			Description: description.Trim(),
			RetentionPolicy: retentionPolicy,
			Tags: tags,
			ChangeHistory: changeHistory);
	}
}
