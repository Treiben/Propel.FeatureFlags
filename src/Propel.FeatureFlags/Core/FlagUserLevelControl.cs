namespace Propel.FeatureFlags.Core;

public class FlagUserLevelControl
{
	public List<string> EnabledUsers { get; set; } = [];
	public List<string> DisabledUsers { get; set; } = [];
	public int PercentageEnabled { get; set; }

	public static FlagUserLevelControl Unrestricted => new()
	{
		EnabledUsers = [],
		DisabledUsers = [],
		PercentageEnabled = 0
	};

	public bool IsUserEnabled(string userId)
	{
		if (DisabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
		{
			return false;
		}
		if (EnabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
		{
			return true;
		}
		return false; // User not explicitly enabled or disabled
	}

	public bool IsUserExplicitlySet(string userId)
	{
		return EnabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase) 
			|| DisabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase);
	}

	public bool EnableUser(string userId)
	{
		if (DisabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
		{
			DisabledUsers.Remove(userId);
		}
		if (!EnabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
		{
			EnabledUsers.Add(userId);
			return true;
		}
		return false; // User was already enabled
	}

	public bool DisableUser(string userId)
	{
		if (EnabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
		{
			EnabledUsers.Remove(userId);
		}
		if (!DisabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
		{
			DisabledUsers.Add(userId);
			return true;
		}
		return false; // User was already disabled
	}

	public bool RemoveUser(string userId)
	{
		var removed = false;
		if (EnabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
		{
			EnabledUsers.Remove(userId);
			removed = true;
		}
		if (DisabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
		{
			DisabledUsers.Remove(userId);
			removed = true;
		}
		return removed; // Return true if user was found and removed from either list
	}

	public void ClearAll()
	{
		EnabledUsers.Clear();
		DisabledUsers.Clear();
		PercentageEnabled = 0;
	}

	public (bool, uint) IsInUserPercentageRollout(string key, string userId)
	{
		if (PercentageEnabled <= 0)
		{
			return (false, 0);
		}

		if (PercentageEnabled >= 100)
		{
			return (true, 100);
		}

		var hashInput = $"{key}:user:{userId}";
		var hash = Hasher.ComputeHash(hashInput);
		var percentage = hash % 100;

		var isEnabled = percentage < PercentageEnabled;
		return (isEnabled, percentage);
	}
}
