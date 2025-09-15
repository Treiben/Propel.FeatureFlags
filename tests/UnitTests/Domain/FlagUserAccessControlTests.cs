using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagUserAccessControl_Validation
{
	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	public void Constructor_InvalidRolloutPercentage_ThrowsArgumentException(int invalidPercentage)
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new FlagUserAccessControl(rolloutPercentage: invalidPercentage));
		exception.ParamName.ShouldBe("rolloutPercentage");
	}

	[Fact]
	public void Constructor_ConflictingUsers_ThrowsArgumentException()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "USER2" };
		var blockedUsers = new List<string> { "User1", "user3" };

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new FlagUserAccessControl(allowedUsers, blockedUsers));
	}

	[Fact]
	public void Constructor_FiltersEmptyUsers()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "", "   ", null!, "user2" };

		// Act
		var accessControl = new FlagUserAccessControl(allowedUsers);

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(2);
		accessControl.AllowedUsers.ShouldContain("user1");
		accessControl.AllowedUsers.ShouldContain("user2");
	}

	[Fact]
	public void Constructor_RemovesDuplicateUsers()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "USER1", "user2" };

		// Act
		var accessControl = new FlagUserAccessControl(allowedUsers);

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(2);
		accessControl.AllowedUsers.ShouldContain("user1");
		accessControl.AllowedUsers.ShouldContain("user2");
	}
}

public class FlagUserAccessControl_Unrestricted
{
	[Fact]
	public void Unrestricted_Returns100PercentRollout()
	{
		// Act
		var accessControl = FlagUserAccessControl.Unrestricted;

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(0);
		accessControl.BlockedUsers.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(100);
	}
}

public class FlagUserAccessControl_HasAccessRestrictions
{
	[Fact]
	public void HasAccessRestrictions_NoRestrictions_ReturnsFalse()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(rolloutPercentage: 100);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(50)]
	[InlineData(99)]
	public void HasAccessRestrictions_RolloutLessThan100_ReturnsTrue(int percentage)
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(rolloutPercentage: percentage);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeTrue();
	}

	[Fact]
	public void HasAccessRestrictions_WithUserLists_ReturnsTrue()
	{
		// Arrange
		var withAllowed = new FlagUserAccessControl(allowedUsers: ["user1"], rolloutPercentage: 100);
		var withBlocked = new FlagUserAccessControl(blockedUsers: ["user1"], rolloutPercentage: 100);

		// Act & Assert
		withAllowed.HasAccessRestrictions().ShouldBeTrue();
		withBlocked.HasAccessRestrictions().ShouldBeTrue();
	}
}

public class FlagUserAccessControl_EvaluateUserAccess
{
	[Fact]
	public void EvaluateUserAccess_ExplicitlyBlocked_ReturnsDenied()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(blockedUsers: ["blocked-user"]);

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("BLOCKED-USER", "test-flag");

		// Assert
		result.ShouldBe(UserAccessResult.Denied);
		reason.ShouldBe("User explicitly blocked");
	}

	[Fact]
	public void EvaluateUserAccess_ExplicitlyAllowed_ReturnsAllowed()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(allowedUsers: ["allowed-user"]);

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("ALLOWED-USER", "test-flag");

		// Assert
		result.ShouldBe(UserAccessResult.Allowed);
		reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public void EvaluateUserAccess_ZeroRollout_ReturnsDenied()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(rolloutPercentage: 0);

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("any-user", "test-flag");

		// Assert
		result.ShouldBe(UserAccessResult.Denied);
		reason.ShouldBe("Access restricted to all users");
	}

	[Fact]
	public void EvaluateUserAccess_FullRollout_ReturnsAllowed()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(rolloutPercentage: 100);

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("any-user", "test-flag");

		// Assert
		result.ShouldBe(UserAccessResult.Allowed);
		reason.ShouldBe("Access unrestricted to all users");
	}

	[Fact]
	public void EvaluateUserAccess_PartialRollout_UsesConsistentHashing()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(rolloutPercentage: 50);
		var userId = "test-user";
		var flagKey = "test-flag";

		// Act
		var (result1, _) = accessControl.EvaluateUserAccess(userId, flagKey);
		var (result2, _) = accessControl.EvaluateUserAccess(userId, flagKey);

		// Assert - Same user/flag should get consistent results
		result1.ShouldBe(result2);
	}

	[Fact]
	public void EvaluateUserAccess_DifferentFlags_MayHaveDifferentResults()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(rolloutPercentage: 50);
		var userId = "test-user";

		// Act
		var (result1, _) = accessControl.EvaluateUserAccess(userId, "flag-one");
		var (result2, _) = accessControl.EvaluateUserAccess(userId, "flag-two");

		// Assert - Different flags may produce different results due to hashing
		result1.ShouldBeOneOf(UserAccessResult.Allowed, UserAccessResult.Denied);
		result2.ShouldBeOneOf(UserAccessResult.Allowed, UserAccessResult.Denied);
	}
}

public class FlagUserAccessControl_FluentInterface
{
	[Fact]
	public void WithAllowedUser_AddsUserToAllowedList()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl();

		// Act
		var result = accessControl.WithAllowedUser("new-user");

		// Assert
		result.ShouldNotBe(accessControl);
		result.AllowedUsers.ShouldContain("new-user");
	}

	[Fact]
	public void WithAllowedUser_AlreadyAllowed_ReturnsSameInstance()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(allowedUsers: ["existing-user"]);

		// Act
		var result = accessControl.WithAllowedUser("existing-user");

		// Assert
		result.ShouldBe(accessControl);
	}

	[Fact]
	public void WithBlockedUser_MovesFromAllowedToBlocked()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(allowedUsers: ["user1", "user2"]);

		// Act
		var result = accessControl.WithBlockedUser("user1");

		// Assert
		result.AllowedUsers.ShouldNotContain("user1");
		result.BlockedUsers.ShouldContain("user1");
		result.AllowedUsers.ShouldContain("user2");
	}

	[Fact]
	public void WithoutUser_RemovesFromBothLists()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(
			allowedUsers: ["user1", "user2"],
			blockedUsers: ["user3"]);

		// Act
		var result = accessControl.WithoutUser("user1").WithoutUser("user3");

		// Assert
		result.AllowedUsers.ShouldBe(new[] { "user2" });
		result.BlockedUsers.ShouldBeEmpty();
	}

	[Fact]
	public void WithRolloutPercentage_UpdatesPercentage()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl(rolloutPercentage: 50);

		// Act
		var result = accessControl.WithRolloutPercentage(75);

		// Assert
		result.ShouldNotBe(accessControl);
		result.RolloutPercentage.ShouldBe(75);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	public void WithRolloutPercentage_InvalidPercentage_ThrowsArgumentException(int invalidPercentage)
	{
		// Arrange
		var accessControl = new FlagUserAccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.WithRolloutPercentage(invalidPercentage));
	}

	[Fact]
	public void FluentInterface_ChainedMethods_WorksCorrectly()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl();

		// Act
		var result = accessControl
			.WithAllowedUser("user1")
			.WithBlockedUser("user2")
			.WithRolloutPercentage(50);

		// Assert
		result.AllowedUsers.ShouldBe(new[] { "user1" });
		result.BlockedUsers.ShouldBe(new[] { "user2" });
		result.RolloutPercentage.ShouldBe(50);
	}

	[Fact]
	public void FluentInterface_MovingUserBetweenLists_ResolvesCorrectly()
	{
		// Arrange
		var accessControl = new FlagUserAccessControl();

		// Act
		var result = accessControl
			.WithAllowedUser("user1")
			.WithBlockedUser("user1") // Move to blocked
			.WithAllowedUser("user1"); // Move back to allowed

		// Assert
		result.AllowedUsers.ShouldBe(new[] { "user1" });
		result.BlockedUsers.ShouldBeEmpty();
	}
}

public class FlagUserAccessControl_InvalidInputs
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithAllowedUser_InvalidUserId_ThrowsArgumentException(string? invalidUserId)
	{
		// Arrange
		var accessControl = new FlagUserAccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.WithAllowedUser(invalidUserId!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithBlockedUser_InvalidUserId_ThrowsArgumentException(string? invalidUserId)
	{
		// Arrange
		var accessControl = new FlagUserAccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.WithBlockedUser(invalidUserId!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void IsUserExplicitlyManaged_InvalidUserId_ReturnsFalse(string? invalidUserId)
	{
		// Arrange
		var accessControl = new FlagUserAccessControl();

		// Act & Assert
		accessControl.IsUserExplicitlyManaged(invalidUserId!).ShouldBeFalse();
	}
}