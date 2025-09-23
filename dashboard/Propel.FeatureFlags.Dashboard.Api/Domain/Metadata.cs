using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public class Metadata
{
	public required FlagIdentifier FlagIdentifier { get; set; }
	public required string Name { get; set; }
	public required string Description { get; set; }
	public RetentionPolicy Retention { get; set; } = RetentionPolicy.ApplicationDefault;
	public Dictionary<string, string> Tags { get; set; } = [];
	public AuditTrail Created { get; set; } = AuditTrail.FlagCreated();
	public AuditTrail? LastModified { get; set; } = default;

	public static Metadata Create(FlagIdentifier identifier, string name, string description)
	{
		var flag = new Metadata
		{
			FlagIdentifier = identifier ?? throw new ArgumentNullException(nameof(identifier)),
			Name = name ?? throw new ArgumentNullException(nameof(name)),
			Description = description ?? string.Empty,
			Retention = identifier.Scope == Scope.Global ? RetentionPolicy.Global : RetentionPolicy.ApplicationDefault
		};

		return flag;
	}
}
