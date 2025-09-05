namespace Propel.FeatureFlags.Core;

public class FlagAuditRecord
{
	public DateTime CreatedAt { get; }
	public DateTime? ModifiedAt { get; }
	public string CreatedBy { get; }
	public string ModifiedBy { get; }

	internal FlagAuditRecord(DateTime createdAt, string createdBy, DateTime? modifiedAt = null, string? modifiedBy = null)
	{
		CreatedAt = createdAt;
		CreatedBy = createdBy ?? string.Empty;
		ModifiedAt = modifiedAt;
		ModifiedBy = modifiedBy ?? string.Empty;
	}


	public static FlagAuditRecord NewFlag(string? createdBy = null)
	{
		var timestamp = DateTime.UtcNow;
		var creator = ValidateAndNormalizeUser(createdBy) ?? "system";
		
		return new FlagAuditRecord(
			createdAt: timestamp,
			createdBy: creator,
			modifiedAt: null,
			modifiedBy: null);
	}

	// This method is used to load records from persistent storage because it skips validation
	public static FlagAuditRecord LoadRecord(DateTime createdAt, string createdBy,
								DateTime? modifiedAt, string? modifiedBy)
	{
		return new FlagAuditRecord(
			createdAt: createdAt,
			createdBy: createdBy,
			modifiedAt: modifiedAt,
			modifiedBy: modifiedBy);
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

		return new FlagAuditRecord(createdAt, validatedCreatedBy, modifiedAt, validatedModifiedBy);
	}

	public FlagAuditRecord WithModification(string modifiedBy, DateTime? modificationTime = null)
	{
		var modificationTimestamp = modificationTime ?? DateTime.UtcNow;
		var validatedModifiedBy = ValidateAndNormalizeUser(modifiedBy) ?? "unknown";

		// Validate modification timestamp
		if (modificationTimestamp < CreatedAt)
		{
			throw new ArgumentException("Modification time cannot be before creation time.", nameof(modificationTime));
		}

		if (modificationTimestamp > DateTime.UtcNow.AddMinutes(1))
		{
			throw new ArgumentException("Modification time cannot be in the future.", nameof(modificationTime));
		}

		return new FlagAuditRecord(
			CreatedAt,
			CreatedBy,
			modificationTimestamp,
			validatedModifiedBy);
	}

	public bool HasBeenModified()
	{
		return ModifiedAt.HasValue && !string.IsNullOrEmpty(ModifiedBy);
	}

	public TimeSpan GetAge()
	{
		return DateTime.UtcNow - CreatedAt;
	}

	public TimeSpan? GetTimeSinceLastModification()
	{
		return ModifiedAt.HasValue ? DateTime.UtcNow - ModifiedAt.Value : null;
	}

	public (DateTime effectiveTimestamp, string effectiveUser) GetLastActivity()
	{
		return ModifiedAt.HasValue && !string.IsNullOrEmpty(ModifiedBy)
			? (ModifiedAt.Value, ModifiedBy)
			: (CreatedAt, CreatedBy);
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