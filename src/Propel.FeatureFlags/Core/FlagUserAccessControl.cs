namespace Propel.FeatureFlags.Core;

public class FlagUserAccessControl
{
	public List<string> AllowedUsers { get; }
	public List<string> BlockedUsers { get; }
	public int RolloutPercentage { get; }

	public FlagUserAccessControl(List<string>? allowedUsers = null, List<string>? blockedUsers = null, int rolloutPercentage = 0)
	{
		if (rolloutPercentage < 0 || rolloutPercentage > 100)
		{
			throw new ArgumentException("Rollout percentage must be between 0 and 100.", nameof(rolloutPercentage));
		}

		RolloutPercentage = rolloutPercentage;

		AllowedUsers = ValidateAndNormalizeUserList(allowedUsers, nameof(allowedUsers));
		BlockedUsers = ValidateAndNormalizeUserList(blockedUsers, nameof(blockedUsers));

		var conflicts = AllowedUsers.Intersect(BlockedUsers, StringComparer.OrdinalIgnoreCase).ToList();
		if (conflicts.Count > 0)
		{
			throw new ArgumentException($"Users cannot be in both allowed and blocked lists: {string.Join(", ", conflicts)}");
		}
	}

	public static FlagUserAccessControl Unrestricted => new(rolloutPercentage: 100);

	public bool HasAccessRestrictions()
	{
		return AllowedUsers.Count > 0 || BlockedUsers.Count > 0 || RolloutPercentage < 100;
	}

	public (UserAccessResult result, string reason) EvaluateUserAccess(string userId, string flagKey)
	{
		if (string.IsNullOrWhiteSpace(userId))
		{
			return (UserAccessResult.Denied, "User ID is required");
		}

		var normalizedUserId = userId.Trim();

		// Check if user is explicitly blocked (takes precedence)
		if (IsUserBlocked(normalizedUserId))
		{
			return (UserAccessResult.Denied, "User explicitly blocked");
		}

		// Check if user is explicitly allowed
		if (IsUserAllowed(normalizedUserId))
		{
			return (UserAccessResult.Allowed, "User explicitly allowed");
		}

		// If no explicit settings and no percentage rollout, deny access
		if (RolloutPercentage <= 0)
		{
			return (UserAccessResult.Denied, "Access restricted to all users");
		}

		if (RolloutPercentage >= 100)
		{
			return (UserAccessResult.Allowed, "Access unrestricted to all users");
		}

		// Check percentage rollout
		var (inRollout, userPercentage) = IsUserInRollout(flagKey, normalizedUserId);
		if (inRollout)
		{
			return (UserAccessResult.Allowed, $"User in rollout: {userPercentage}% < {RolloutPercentage}%");
		}

		return (UserAccessResult.Denied, $"User not in rollout: {userPercentage}% >= {RolloutPercentage}%");
	}

	public bool IsUserExplicitlyManaged(string userId)
	{
		if (string.IsNullOrWhiteSpace(userId))
		{
			return false;
		}

		var normalizedUserId = userId.Trim();
		return IsUserAllowed(normalizedUserId) || IsUserBlocked(normalizedUserId);
	}

	public FlagUserAccessControl WithAllowedUser(string userId)
	{
		if (string.IsNullOrWhiteSpace(userId))
		{
			throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
		}

		var normalizedUserId = userId.Trim();
		if (IsUserAllowed(normalizedUserId))
		{
			return this; // Already allowed, return same instance
		}

		var newAllowedUsers = AllowedUsers.ToList();
		newAllowedUsers.Add(normalizedUserId);

		var newBlockedUsers = BlockedUsers.Where(u => !string.Equals(u, normalizedUserId, StringComparison.OrdinalIgnoreCase)).ToList();

		return new FlagUserAccessControl(newAllowedUsers, newBlockedUsers, RolloutPercentage);
	}

	public FlagUserAccessControl WithBlockedUser(string userId)
	{
		if (string.IsNullOrWhiteSpace(userId))
		{
			throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
		}

		var normalizedUserId = userId.Trim();
		if (IsUserBlocked(normalizedUserId))
		{
			return this; // Already blocked, return same instance
		}

		var newBlockedUsers = BlockedUsers.ToList();
		newBlockedUsers.Add(normalizedUserId);

		var newAllowedUsers = AllowedUsers.Where(u => !string.Equals(u, normalizedUserId, StringComparison.OrdinalIgnoreCase)).ToList();

		return new FlagUserAccessControl(newAllowedUsers, newBlockedUsers, RolloutPercentage);
	}

	public FlagUserAccessControl WithoutUser(string userId)
	{
		if (string.IsNullOrWhiteSpace(userId))
		{
			throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
		}

		var normalizedUserId = userId.Trim();
		var newAllowedUsers = AllowedUsers.Where(u => !string.Equals(u, normalizedUserId, StringComparison.OrdinalIgnoreCase)).ToList();
		var newBlockedUsers = BlockedUsers.Where(u => !string.Equals(u, normalizedUserId, StringComparison.OrdinalIgnoreCase)).ToList();

		if (newAllowedUsers.Count == AllowedUsers.Count && newBlockedUsers.Count == BlockedUsers.Count)
		{
			return this; // No changes needed
		}

		return new FlagUserAccessControl(newAllowedUsers, newBlockedUsers, RolloutPercentage);
	}

	public FlagUserAccessControl WithRolloutPercentage(int percentage)
	{
		if (percentage < 0 || percentage > 100)
		{
			throw new ArgumentException("Rollout percentage must be between 0 and 100.", nameof(percentage));
		}

		if (RolloutPercentage == percentage)
		{
			return this; // No change needed
		}

		return new FlagUserAccessControl([.. AllowedUsers], [.. BlockedUsers], percentage);
	}

	private bool IsUserAllowed(string userId)
	{
		return AllowedUsers.Contains(userId, StringComparer.OrdinalIgnoreCase);
	}

	private bool IsUserBlocked(string userId)
	{
		return BlockedUsers.Contains(userId, StringComparer.OrdinalIgnoreCase);
	}

	private (bool inRollout, uint userPercentage) IsUserInRollout(string flagKey, string userId)
	{
		if (RolloutPercentage <= 0)
		{
			return (false, 0);
		}

		if (RolloutPercentage >= 100)
		{
			return (true, 100);
		}

		var hashInput = $"{flagKey}:user:{userId}";
		var hash = Hasher.ComputeHash(hashInput);
		var userPercentage = (uint)(hash % 100);

		var inRollout = userPercentage < RolloutPercentage;
		return (inRollout, userPercentage);
	}

	private static List<string> ValidateAndNormalizeUserList(List<string>? userList, string parameterName)
	{
		if (userList == null || userList.Count == 0)
		{
			return [];
		}

		var normalizedUsers = new List<string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var userId in userList)
		{
			if (string.IsNullOrWhiteSpace(userId))
			{
				continue; // Skip null/empty entries
			}

			var normalizedUserId = userId.Trim();
			if (seen.Add(normalizedUserId))
			{
				normalizedUsers.Add(normalizedUserId);
			}
		}

		return normalizedUsers;
	}
}

public enum UserAccessResult
{
	Allowed,
	Denied
}