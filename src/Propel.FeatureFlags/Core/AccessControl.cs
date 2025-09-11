namespace Propel.FeatureFlags.Core;

public enum AccessResult
{
	Allowed,
	Denied
}

public class AccessControl
{
	public List<string> Allowed { get; }
	public List<string> Blocked { get; }
	public int RolloutPercentage { get; }

	public AccessControl(List<string>? allowed = null, List<string>? blocked = null, int rolloutPercentage = 0)
	{
		if (rolloutPercentage < 0 || rolloutPercentage > 100)
		{
			throw new ArgumentException("Rollout percentage must be between 0 and 100.", nameof(rolloutPercentage));
		}

		RolloutPercentage = rolloutPercentage;

		Allowed = ValidateAndNormalizeList(allowed);
		Blocked = ValidateAndNormalizeList(blocked);

		var conflicts = Allowed.Intersect(Blocked, StringComparer.OrdinalIgnoreCase).ToList();
		if (conflicts.Count > 0)
		{
			throw new ArgumentException($"Same entity cannot be in both allowed and blocked lists: {string.Join(", ", conflicts)}");
		}
	}

	public static AccessControl Unrestricted => new(rolloutPercentage: 100);

	public bool HasAccessRestrictions()
	{
		return Allowed.Count > 0 || Blocked.Count > 0 || RolloutPercentage < 100;
	}

	public (AccessResult result, string reason) EvaluateAccess(string id, string flagKey)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return (AccessResult.Denied, "ID is required");
		}

		var normalizedId = id.Trim();

		// Check if user is explicitly blocked (takes precedence)
		if (IsBlockedFor(normalizedId))
		{
			return (AccessResult.Denied, "Access is blocked");
		}

		// Check if user is explicitly allowed
		if (IsAllowedFor(normalizedId))
		{
			return (AccessResult.Allowed, "Access is allowed");
		}

		// Check percentage rollout
		if (RolloutPercentage <= 0)
		{
			return (AccessResult.Denied, "Access restricted to all");
		}

		if (RolloutPercentage >= 100)
		{
			return (AccessResult.Allowed, "Access unrestricted to all");
		}

		// Check percentage rollout
		var (inRollout, percentage) = IsInRollout(flagKey, normalizedId);
		if (inRollout)
		{
			return (AccessResult.Allowed, $"Id is in rollout: {percentage}% < {RolloutPercentage}%");
		}

		return (AccessResult.Denied, $"Id is not in rollout: {percentage}% >= {RolloutPercentage}%");
	}

	public AccessControl AllowAccessFor(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new ArgumentException("ID cannot be null or empty.", nameof(id));
		}

		var normalizedId = id.Trim();
		if (IsAllowedFor(normalizedId))
		{
			return this; // Already allowed, return same instance
		}

		var newAllowed = Allowed.ToList();
		newAllowed.Add(normalizedId);

		var newBlocked = Blocked.Where(u => !string.Equals(u, normalizedId, StringComparison.OrdinalIgnoreCase)).ToList();

		return new AccessControl(newAllowed, newBlocked, RolloutPercentage);
	}

	public AccessControl BlockAccessFor(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new ArgumentException("ID cannot be null or empty.", nameof(id));
		}

		var normalizedId = id.Trim();
		if (IsBlockedFor(normalizedId))
		{
			return this; // Already blocked, return same instance
		}

		var newBlockedList = Blocked.ToList();
		newBlockedList.Add(normalizedId);

		var newAllowedList = Allowed.Where(u => !string.Equals(u, normalizedId, StringComparison.OrdinalIgnoreCase)).ToList();

		return new AccessControl(newAllowedList, newBlockedList, RolloutPercentage);
	}

	public AccessControl Remove(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new ArgumentException("ID cannot be null or empty.", nameof(id));
		}

		var normalizedId = id.Trim();
		var newAllowedList = Allowed.Where(u => !string.Equals(u, normalizedId, StringComparison.OrdinalIgnoreCase)).ToList();
		var newBlockedList = Blocked.Where(u => !string.Equals(u, normalizedId, StringComparison.OrdinalIgnoreCase)).ToList();

		if (newAllowedList.Count == Allowed.Count && newBlockedList.Count == Blocked.Count)
		{
			return this; // No changes needed
		}

		return new AccessControl(newAllowedList, newBlockedList, RolloutPercentage);
	}

	public AccessControl WithRolloutPercentage(int percentage)
	{
		if (percentage < 0 || percentage > 100)
		{
			throw new ArgumentException("Rollout percentage must be between 0 and 100.", nameof(percentage));
		}

		if (RolloutPercentage == percentage)
		{
			return this; 
		}

		return new AccessControl([.. Allowed], [.. Blocked], percentage);
	}

	private bool IsAllowedFor(string id)
	{
		return Allowed.Contains(id, StringComparer.OrdinalIgnoreCase);
	}

	private bool IsBlockedFor(string id)
	{
		return Blocked.Contains(id, StringComparer.OrdinalIgnoreCase);
	}

	private (bool inRollout, uint percentage) IsInRollout(string flagKey, string id)
	{
		if (RolloutPercentage <= 0)
		{
			return (false, 0);
		}

		if (RolloutPercentage >= 100)
		{
			return (true, 100);
		}

		var hashInput = $"{flagKey}:access:{id}";
		var hash = Hasher.ComputeHash(hashInput);
		var percentage = (uint)(hash % 100);

		var inRollout = percentage < RolloutPercentage;
		return (inRollout, percentage);
	}

	private static List<string> ValidateAndNormalizeList(List<string>? list)
	{
		if (list == null || list.Count == 0)
		{
			return [];
		}

		var normalizedList = new List<string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var id in list)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				continue; 
			}

			var normalizedId = id.Trim();
			if (seen.Add(normalizedId))
			{
				normalizedList.Add(normalizedId);
			}
		}

		return normalizedList;
	}
}