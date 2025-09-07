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
		// Validate creation timestamp
		if (createdAt == DateTime.MinValue || createdAt > DateTime.UtcNow.AddMinutes(1))
		{
			throw new ArgumentException("Created timestamp must be a valid past or current time.", nameof(createdAt));
		}

		// Validate modification timestamp
		var normalizedModifiedAt = NormalizeModifiedDate(modifiedAt);
		if (normalizedModifiedAt.HasValue)
		{
			if (normalizedModifiedAt.Value < createdAt)
			{
				throw new ArgumentException("Modified timestamp cannot be before creation timestamp.", nameof(modifiedAt));
			}

			if (normalizedModifiedAt.Value > DateTime.UtcNow.AddMinutes(1))
			{
				throw new ArgumentException("Modified timestamp cannot be in the future.", nameof(modifiedAt));
			}
		}

		CreatedAt = createdAt;
		ModifiedAt = normalizedModifiedAt;	
		CreatedBy = ValidateAndNormalizeUser(createdBy) ?? "unknown";
		ModifiedBy = modifiedAt.HasValue ? ValidateAndNormalizeUser(modifiedBy) : null;
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

	private static DateTime? NormalizeModifiedDate(DateTime? date)
	{
		if (date.HasValue && (date.Value == DateTime.MinValue || date.Value == DateTime.MaxValue))
			return null;
		return date;
	}
}