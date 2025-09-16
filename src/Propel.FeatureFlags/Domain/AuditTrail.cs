using Propel.FeatureFlags.Helpers;

namespace Propel.FeatureFlags.Domain;

public class AuditTrail
{
	public DateTime Timestamp { get; }
	public string Actor { get; }

	public string Reason { get; set; }

	// This method is used to create new audit records in valid state
	public AuditTrail(DateTime timestamp, string actor, string reason = "Not specified")
	{
		var utcChangedAt = DateTimeHelpers.NormalizeToUtc(dateTime: timestamp, utcReplacementDt: DateTime.UtcNow);

		// Validate creation timestamp
		if (utcChangedAt > DateTime.UtcNow.AddMinutes(1))
		{
			throw new ArgumentException("Change timestamp must be a valid past or current time.", nameof(timestamp));
		}

		Timestamp = utcChangedAt;
		Actor = ValidateAndNormalize(actor);
		Reason = ValidateAndNormalize(reason);
	}

	public static AuditTrail FlagCreated(string? createdBy = null)
	{
		var timestamp = DateTime.UtcNow;
		var creator = createdBy ?? "system";

		return new AuditTrail(timestamp: timestamp, actor: creator, reason: "Flag created");
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
