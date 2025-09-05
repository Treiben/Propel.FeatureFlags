namespace Propel.FeatureFlags.Core;

public class FlagAuditRecord
{
	public DateTime CreatedAt { get; }
	public DateTime? ModifiedAt { get; }
	public string CreatedBy { get; }
	public string? ModifiedBy { get; }

	public FlagAuditRecord(DateTime createdAt, string createdBy, DateTime? modifiedAt = null, string? modifiedBy = null)
	{
		CreatedAt = createdAt;
		ModifiedAt = modifiedAt;
		CreatedBy = createdBy;
		ModifiedBy = modifiedBy;
	}

	public static FlagAuditRecord NewFlag(string? createdBy = null)
	{
		var timestamp = DateTime.UtcNow;
		var creator = ValidateAndNormalizeUser(createdBy) ?? "system";

		return new FlagAuditRecord(createdAt: timestamp, createdBy: creator);
	}

	// This method is used to create new audit records in valid state
	public static FlagAuditRecord CreateRecord(
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

		// Validate and normalize users
		var validatedCreatedBy = ValidateAndNormalizeUser(createdBy) ?? "unknown";
		var validatedModifiedBy = modifiedAt.HasValue ? ValidateAndNormalizeUser(modifiedBy) : null;

		return new FlagAuditRecord(
			createdAt: createdAt, 
			createdBy: createdBy, 
			modifiedAt: modifiedAt, 
			modifiedBy: validatedModifiedBy);
	}

	private static string? ValidateAndNormalizeUser(string? user)
	{
		if (string.IsNullOrWhiteSpace(user))
		{
			return null;
		}

		var normalizedUser = user.Trim();
		
		// Validate user identifier format (basic validation)
		if (normalizedUser.Length > 255)
		{
			throw new ArgumentException("User identifier cannot exceed 255 characters.", nameof(user));
		}

		return normalizedUser;
	}
}