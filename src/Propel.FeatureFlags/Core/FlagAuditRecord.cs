namespace Propel.FeatureFlags.Core;

public class FlagAuditRecord
{
	public DateTime CreatedAt { get; }
	public DateTime? ModifiedAt { get; }
	public string CreatedBy { get; }
	public string? ModifiedBy { get; }

	// This method is used to create new audit records in valid state
	public FlagAuditRecord(
		DateTime createdAt,
		string createdBy,
		DateTime? modifiedAt = null,
		string? modifiedBy = null)
	{
		var utcCreatedAt = NormalizeToUtc(createdAt);

		// Validate creation timestamp
		if (utcCreatedAt.HasValue == false || utcCreatedAt > DateTime.UtcNow.AddMinutes(1))
		{
			throw new ArgumentException("Created timestamp must be a valid past or current time.", nameof(createdAt));
		}

		// Validate modification timestamp
		var utcModifiedAt = NormalizeToUtc(modifiedAt);
		if (utcModifiedAt.HasValue)
		{
			if (utcModifiedAt.Value < utcCreatedAt)
			{
				throw new ArgumentException("Modified timestamp cannot be before creation timestamp.", nameof(modifiedAt));
			}

			if (utcModifiedAt.Value > DateTime.UtcNow.AddMinutes(1))
			{
				throw new ArgumentException("Modified timestamp cannot be in the future.", nameof(modifiedAt));
			}
		}

		CreatedAt = utcCreatedAt!.Value;
		ModifiedAt = utcModifiedAt;	
		CreatedBy = ValidateAndNormalizeUser(createdBy) ?? "unknown";
		ModifiedBy = utcModifiedAt.HasValue ? ValidateAndNormalizeUser(modifiedBy) : null;
	}

	public static FlagAuditRecord NewFlag(string? createdBy = null)
	{
		var timestamp = DateTime.UtcNow;
		var creator = createdBy ?? "system";

		return new FlagAuditRecord(createdAt: timestamp, createdBy: creator);
	}

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