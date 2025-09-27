using Knara.UtcStrict;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public record AuditTrail(UtcDateTime Timestamp, string Actor, string Action, string Notes)
{
	public static AuditTrail FlagCreated(string? username = null, string? notes = null)
	{
		var timestamp = UtcDateTime.UtcNow;
		var action = "flag-created";
		var creator = username ?? "system";

		notes = ValidateAndNormalize(notes ?? "Flag added to the system from the dashboard");
		return new AuditTrail(Timestamp: timestamp, Actor: creator, Action: action, Notes: notes);
	}

	public static AuditTrail FlagModified(string? username = null, string? notes = null)
	{
		var timestamp = UtcDateTime.UtcNow;
		var action = "flag-modified";
		var creator = username ?? "system";

		notes = ValidateAndNormalize(notes ?? "Flag modified from the dashboard");
		return new AuditTrail(Timestamp: timestamp, Actor: creator, Action: action, Notes: notes);
	}

	public static AuditTrail FlagDeleted(string? username = null, string? notes = null)
	{
		var timestamp = UtcDateTime.UtcNow;
		var action = "flag-deleted";
		var creator = username ?? "system";

		notes = ValidateAndNormalize(notes ?? "Flag deleted from the dashboard");
		return new AuditTrail(Timestamp: timestamp, Actor: creator, Action: action, Notes: notes);
	}

	public static bool operator >=(AuditTrail left, AuditTrail right) => left.Timestamp >= right.Timestamp;
	public static bool operator <=(AuditTrail left, AuditTrail right) => left.Timestamp <= right.Timestamp;

	private static string ValidateAndNormalize(string field)
	{
		if (string.IsNullOrWhiteSpace(field))
		{
			return string.Empty;
		}

		var normalizedUser = field!.Trim();

		// Validate user identifier format (basic validation)
		if (normalizedUser.Length > 255)
		{
			throw new ArgumentException("User identifier cannot exceed 255 characters.", nameof(field));
		}

		return normalizedUser;
	}
}
