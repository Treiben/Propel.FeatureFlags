using Knara.UtcStrict;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public class AuditTrail
{
	public UtcDateTime Timestamp { get; }
	public string Actor { get; }
	public string Action { get; }
	public string Reason { get; set; }

	// This method is used to create new audit records in valid state
	public AuditTrail(UtcDateTime timestamp, string actor,
		string? action = null, string reason = "Not specified")
	{
		// Validate creation timestamp
		if (timestamp > UtcDateTime.UtcNow)
		{
			throw new ArgumentException("Change timestamp must be a valid past or current time.", nameof(timestamp));
		}

		Timestamp = timestamp;
		Actor = ValidateAndNormalize(actor);
		Action = ValidateAndNormalize(action ?? string.Empty);
		Reason = ValidateAndNormalize(reason);
	}

	public static AuditTrail FlagCreated(string? createdBy = null)
	{
		var timestamp = UtcDateTime.UtcNow;
		var action = "flag-created";
		var creator = createdBy ?? "system";

		return new AuditTrail(timestamp: timestamp, actor: creator, action: action, reason: "Flag created");
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
