namespace Propel.FeatureFlags.Domain;

public class Audit
{
	public DateTime Timestamp { get; }
	public string? Actor { get; }

	// This method is used to create new audit records in valid state
	public Audit(DateTime timestamp, string actor)
	{
		var utcChangedAt = NormalizeToUtc(timestamp);

		// Validate creation timestamp
		if (utcChangedAt.HasValue == false || utcChangedAt > DateTime.UtcNow.AddMinutes(1))
		{
			throw new ArgumentException("Change timestamp must be a valid past or current time.", nameof(timestamp));
		}

		Timestamp = utcChangedAt!.Value;
		Actor = ValidateAndNormalizeUser(actor);
	}

	public static Audit FlagCreated(string? createdBy = null)
	{
		var timestamp = DateTime.UtcNow;
		var creator = createdBy ?? "system";

		return new Audit(timestamp: timestamp, actor: creator);
	}

	public static bool operator >=(Audit left, Audit right) => left.Timestamp >= right.Timestamp;
	public static bool operator <=(Audit left, Audit right) => left.Timestamp <= right.Timestamp;

	private static string? ValidateAndNormalizeUser(string? user)
	{
		if (string.IsNullOrWhiteSpace(user))
		{
			return null;
		}

		var normalizedUser = user!.Trim();

		// Validate user identifier format (basic validation)
		if (normalizedUser.Length > 255)
		{
			throw new ArgumentException("User identifier cannot exceed 255 characters.", nameof(user));
		}

		return normalizedUser;
	}

	private static DateTime? NormalizeToUtc(DateTime? dateTime)
	{
		if (!dateTime.HasValue || dateTime.Value == DateTime.MinValue || dateTime.Value == DateTime.MaxValue)
			return null;

		if (dateTime!.Value.Kind == DateTimeKind.Utc)
		{
			return dateTime;
		}
		return dateTime.Value.ToUniversalTime();
	}

}
