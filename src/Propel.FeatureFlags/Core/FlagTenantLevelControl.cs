namespace Propel.FeatureFlags.Core;

public class FlagTenantLevelControl
{
	public List<string> EnabledTenants { get; set; } = [];
	public List<string> DisabledTenants { get; set; } = [];
	public int PercentageEnabled { get; set; }

	public static FlagTenantLevelControl Unrestricted => new FlagTenantLevelControl
	{
		EnabledTenants = [],
		DisabledTenants = [],
		PercentageEnabled = 0
	};

	public bool IsTenantEnabled(string tenantId)
	{
		if (DisabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
		{
			return false;
		}
		if (EnabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
		{
			return true;
		}
		return false; // Tenant not explicitly enabled or disabled
	}
	public bool IsTenantExplicitlySet(string tenantId)
	{
		return EnabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase) 
			|| DisabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
	}
	public bool EnableTenant(string tenantId)
	{
		if (DisabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
		{
			DisabledTenants.Remove(tenantId);
		}
		if (!EnabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
		{
			EnabledTenants.Add(tenantId);
			return true;
		}
		return false; // Tenant was already enabled
	}
	public bool DisableTenant(string tenantId)
	{
		if (EnabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
		{
			EnabledTenants.Remove(tenantId);
		}
		if (!DisabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
		{
			DisabledTenants.Add(tenantId);
			return true;
		}
		return false; // Tenant was already disabled
	}
	public bool RemoveTenant(string tenantId)
	{
		var removed = false;
		if (EnabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
		{
			EnabledTenants.Remove(tenantId);
			removed = true;
		}
		if (DisabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase))
		{
			DisabledTenants.Remove(tenantId);
			removed = true;
		}
		return removed; // True if tenant was found and removed from either list
	}
	public void ClearAll()
	{
		EnabledTenants.Clear();
		DisabledTenants.Clear();
		PercentageEnabled = 0;
	}
	public bool IsInTenantPercentageRollout(string key, string tenantId)
	{
		if (PercentageEnabled <= 0)
		{
			return false;
		}

		if (PercentageEnabled >= 100)
		{
			return true;
		}

		// Use consistent hashing to ensure same tenant always gets same result
		var hashInput = $"{key}:tenant:{tenantId}";
		var hash = Hasher.ComputeHash(hashInput);
		var percentage = hash % 100;
		var isAllowed = percentage < PercentageEnabled;

		return isAllowed;
	}
}
