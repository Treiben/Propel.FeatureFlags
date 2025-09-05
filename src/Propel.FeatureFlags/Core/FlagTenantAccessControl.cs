namespace Propel.FeatureFlags.Core;

public class FlagTenantAccessControl
{
	public IReadOnlyList<string> AllowedTenants { get; }
	public IReadOnlyList<string> BlockedTenants { get; }
	public int RolloutPercentage { get; }

	// Internal constructor to prevent direct instantiation
	internal FlagTenantAccessControl(List<string>? allowedTenants = null, List<string>? blockedTenants = null, int rolloutPercentage = 0)
	{
		AllowedTenants = (allowedTenants ?? []).AsReadOnly();
		BlockedTenants = (blockedTenants ?? []).AsReadOnly();
		RolloutPercentage = rolloutPercentage;
	}

	public static FlagTenantAccessControl Unrestricted => new(rolloutPercentage: 100);

	// This method is used to load access controls from persistent storage because it skips validation
	public static FlagTenantAccessControl LoadAccessControl(List<string>? allowedTenants = null, List<string>? blockedTenants = null, int rolloutPercentage = 0)
	{
		return new FlagTenantAccessControl(allowedTenants, blockedTenants, rolloutPercentage);
	}

	// This method is used to create new access controls in valid state
	public static FlagTenantAccessControl CreateAccessControl(
		List<string>? allowedTenants = null, 
		List<string>? blockedTenants = null, 
		int rolloutPercentage = 0)
	{
		// Validate rollout percentage
		if (rolloutPercentage < 0 || rolloutPercentage > 100)
		{
			throw new ArgumentException("Rollout percentage must be between 0 and 100.", nameof(rolloutPercentage));
		}

		// Validate and normalize tenant lists
		var validatedAllowedTenants = ValidateAndNormalizeTenantList(allowedTenants, nameof(allowedTenants));
		var validatedBlockedTenants = ValidateAndNormalizeTenantList(blockedTenants, nameof(blockedTenants));

		// Check for conflicts between allowed and blocked tenants
		var conflicts = validatedAllowedTenants.Intersect(validatedBlockedTenants, StringComparer.OrdinalIgnoreCase).ToList();
		if (conflicts.Count > 0)
		{
			throw new ArgumentException($"Tenants cannot be in both allowed and blocked lists: {string.Join(", ", conflicts)}");
		}

		return new FlagTenantAccessControl(validatedAllowedTenants, validatedBlockedTenants, rolloutPercentage);
	}

	public bool HasAccessRestrictions()
	{
		return AllowedTenants.Count > 0 || BlockedTenants.Count > 0 || RolloutPercentage < 100;
	}

	public (TenantAccessResult result, string reason) EvaluateTenantAccess(string tenantId, string flagKey)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			return (TenantAccessResult.Denied, "Tenant ID is required");
		}

		var normalizedTenantId = tenantId.Trim();

		// Check if tenant is explicitly blocked (takes precedence)
		if (IsTenantBlocked(normalizedTenantId))
		{
			return (TenantAccessResult.Denied, "Tenant explicitly blocked");
		}

		// Check if tenant is explicitly allowed
		if (IsTenantAllowed(normalizedTenantId))
		{
			return (TenantAccessResult.Allowed, "Tenant explicitly allowed");
		}

		// If no explicit settings and no percentage rollout, deny access
		if (RolloutPercentage <= 0)
		{
			return (TenantAccessResult.Denied, "Access restricted to all tenants");
		}

		if (RolloutPercentage >= 100)
		{
			return (TenantAccessResult.Allowed, "Access unrestricted to all tenants");
		}

		// Check percentage rollout
		var (inRollout, tenantPercentage) = IsTenantInRollout(flagKey, normalizedTenantId);
		if (inRollout)
		{
			return (TenantAccessResult.Allowed, $"Tenant in rollout: {tenantPercentage}% < {RolloutPercentage}%");
		}

		return (TenantAccessResult.Denied, $"Tenant not in rollout: {tenantPercentage}% >= {RolloutPercentage}%");
	}

	public bool IsTenantExplicitlyManaged(string tenantId)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			return false;
		}

		var normalizedTenantId = tenantId.Trim();
		return IsTenantAllowed(normalizedTenantId) || IsTenantBlocked(normalizedTenantId);
	}

	public FlagTenantAccessControl WithAllowedTenant(string tenantId)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(tenantId));
		}

		var normalizedTenantId = tenantId.Trim();
		if (IsTenantAllowed(normalizedTenantId))
		{
			return this; // Already allowed, return same instance
		}

		var newAllowedTenants = AllowedTenants.ToList();
		newAllowedTenants.Add(normalizedTenantId);

		var newBlockedTenants = BlockedTenants.Where(t => !string.Equals(t, normalizedTenantId, StringComparison.OrdinalIgnoreCase)).ToList();

		return new FlagTenantAccessControl(newAllowedTenants, newBlockedTenants, RolloutPercentage);
	}

	public FlagTenantAccessControl WithBlockedTenant(string tenantId)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(tenantId));
		}

		var normalizedTenantId = tenantId.Trim();
		if (IsTenantBlocked(normalizedTenantId))
		{
			return this; // Already blocked, return same instance
		}

		var newBlockedTenants = BlockedTenants.ToList();
		newBlockedTenants.Add(normalizedTenantId);

		var newAllowedTenants = AllowedTenants.Where(t => !string.Equals(t, normalizedTenantId, StringComparison.OrdinalIgnoreCase)).ToList();

		return new FlagTenantAccessControl(newAllowedTenants, newBlockedTenants, RolloutPercentage);
	}

	public FlagTenantAccessControl WithoutTenant(string tenantId)
	{
		if (string.IsNullOrWhiteSpace(tenantId))
		{
			throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(tenantId));
		}

		var normalizedTenantId = tenantId.Trim();
		var newAllowedTenants = AllowedTenants.Where(t => !string.Equals(t, normalizedTenantId, StringComparison.OrdinalIgnoreCase)).ToList();
		var newBlockedTenants = BlockedTenants.Where(t => !string.Equals(t, normalizedTenantId, StringComparison.OrdinalIgnoreCase)).ToList();

		if (newAllowedTenants.Count == AllowedTenants.Count && newBlockedTenants.Count == BlockedTenants.Count)
		{
			return this; // No changes needed
		}

		return new FlagTenantAccessControl(newAllowedTenants, newBlockedTenants, RolloutPercentage);
	}

	public FlagTenantAccessControl WithRolloutPercentage(int percentage)
	{
		if (percentage < 0 || percentage > 100)
		{
			throw new ArgumentException("Rollout percentage must be between 0 and 100.", nameof(percentage));
		}

		if (RolloutPercentage == percentage)
		{
			return this; // No change needed
		}

		return new FlagTenantAccessControl(AllowedTenants.ToList(), BlockedTenants.ToList(), percentage);
	}

	private bool IsTenantAllowed(string tenantId)
	{
		return AllowedTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
	}

	private bool IsTenantBlocked(string tenantId)
	{
		return BlockedTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
	}

	private (bool inRollout, uint tenantPercentage) IsTenantInRollout(string flagKey, string tenantId)
	{
		if (RolloutPercentage <= 0)
		{
			return (false, 0);
		}

		if (RolloutPercentage >= 100)
		{
			return (true, 100);
		}

		var hashInput = $"{flagKey}:tenant:{tenantId}";
		var hash = Hasher.ComputeHash(hashInput);
		var tenantPercentage = (uint)(hash % 100);

		var inRollout = tenantPercentage < RolloutPercentage;
		return (inRollout, tenantPercentage);
	}

	private static List<string> ValidateAndNormalizeTenantList(List<string>? tenantList, string parameterName)
	{
		if (tenantList == null || tenantList.Count == 0)
		{
			return [];
		}

		var normalizedTenants = new List<string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var tenantId in tenantList)
		{
			if (string.IsNullOrWhiteSpace(tenantId))
			{
				continue; // Skip null/empty entries
			}

			var normalizedTenantId = tenantId.Trim();
			if (seen.Add(normalizedTenantId))
			{
				normalizedTenants.Add(normalizedTenantId);
			}
		}

		return normalizedTenants;
	}
}

public enum TenantAccessResult
{
	Allowed,
	Denied
}