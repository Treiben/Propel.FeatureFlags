using Propel.FeatureFlags.Utilities;

namespace Propel.FeatureFlags.Domain;

public enum AccessResult
{
	Allowed,
	Denied
}

/// <summary>
/// Represents an access control mechanism that manages allowed and blocked entities,  as well as percentage-based
/// rollout restrictions.
/// </summary>
/// <remarks>This class provides functionality to define and evaluate access rules for entities  based on explicit
/// allow/deny lists and a rollout percentage. It supports immutable  operations to modify access rules, ensuring thread
/// safety and consistency.  Use the <see cref="Allowed"/> and <see cref="Blocked"/> properties to inspect the  current
/// access rules, and methods such as <see cref="AllowAccessFor(string)"/> and  <see cref="BlockAccessFor(string)"/> to
/// create modified instances with updated rules. The <see cref="RolloutPercentage"/> property determines the percentage
/// of entities  that are granted access when neither explicitly allowed nor blocked.</remarks>
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
		return Allowed.Count > 0 || Blocked.Count > 0 || (RolloutPercentage > 0 && RolloutPercentage < 100);
	}

	public (AccessResult result, string reason) EvaluateAccess(string id, string flagKey)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			return (AccessResult.Denied, "ID is required");
		}

		var normalizedId = id.Trim();

		if (Allowed.Count > 0 || Blocked.Count > 0)
		{
			// If there are explicit allow/block rules, use them
			return EvaluateWithExplicitRules(normalizedId, flagKey);
		}
		else
		{
			// Otherwise, rely solely on percentage rollout
			return EvaluateWithRolloutOnly(normalizedId, flagKey);
		}
	}

	private (AccessResult result, string reason) EvaluateWithRolloutOnly(string normalizedId, string flagKey)
	{
		// Check percentage rollout
		if (RolloutPercentage <= 0)
		{
			return (AccessResult.Denied, "Access restricted to all");
		}
		if (RolloutPercentage >= 100)
		{
			return (AccessResult.Allowed, "Access unrestricted to all");
		}
		var (inRollout, percentage) = IsInRollout(flagKey, normalizedId);
		if (inRollout)
		{
			return (AccessResult.Allowed, $"Id is in rollout: {percentage}% < {RolloutPercentage}%");
		}
		return (AccessResult.Denied, $"Id is not in rollout: {percentage}% >= {RolloutPercentage}%");
	}

	private (AccessResult result, string reason) EvaluateWithExplicitRules(string normalizedId, string flagKey)
	{
		// Check if user is explicitly blocked (takes precedence)
		if (IsBlockedFor(normalizedId))
		{
			return (AccessResult.Denied, "Access is blocked");
		}
		
		if (Allowed.Count == 0)
		{
			// If no one is explicitly allowed, everyone not blocked is allowed
			return (AccessResult.Allowed, "Access is allowed all entities not in blocked list (no explicit allow rules)");
		}

		if (IsAllowedFor(normalizedId))
		{
			// Check if user is explicitly allowed
			return (AccessResult.Allowed, "Access is allowed");
		}

		// If no one is explicitly blocked, everyone not allowed is denied
		return (AccessResult.Denied, "Access is denied to all entities not in allowed list (no explicit block rules)");

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

	private (bool inRollout, int percentage) IsInRollout(string flagKey, string id)
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
		var percentage = hash % 100;

		var inRollout = percentage < RolloutPercentage;
		return (inRollout, (int)percentage);
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