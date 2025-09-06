namespace Propel.FeatureFlags.Core;

public class FlagAuditRecord
{
	public DateTime CreatedAt { get; }
	public DateTime? ModifiedAt { get; }
	public string CreatedBy { get; }
	public string? ModifiedBy { get; }

	public static FlagAuditRecord NewFlag(string? createdBy = null)
	{
		var timestamp = DateTime.UtcNow;
		var creator = createdBy ?? "system";

		return new FlagAuditRecord(createdAt: timestamp, createdBy: creator);
	}

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
		if (modifiedAt.HasValue)
		{
			if (modifiedAt.Value < createdAt)
			{
				throw new ArgumentException("Modified timestamp cannot be before creation timestamp.", nameof(modifiedAt));
			}

			if (modifiedAt.Value > DateTime.UtcNow.AddMinutes(1))
			{
				throw new ArgumentException("Modified timestamp cannot be in the future.", nameof(modifiedAt));
			}
		}

		CreatedAt = createdAt;
		ModifiedAt = modifiedAt;	
		CreatedBy = ValidateAndNormalizeUser(createdBy) ?? "unknown";
		ModifiedBy = modifiedAt.HasValue ? ValidateAndNormalizeUser(modifiedBy) : null;
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
}