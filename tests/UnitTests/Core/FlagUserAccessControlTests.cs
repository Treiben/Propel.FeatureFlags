using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagUserAccessControl_Constructor
{
	[Fact]
	public void If_InternalConstructor_ThenSetsProperties()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "user2" };
		var blockedUsers = new List<string> { "user3", "user4" };
		var rolloutPercentage = 75;

		// Act
		var accessControl = new FlagUserAccessControl(allowedUsers, blockedUsers, rolloutPercentage);

		// Assert
		accessControl.AllowedUsers.ShouldBe(allowedUsers.AsReadOnly());
		accessControl.BlockedUsers.ShouldBe(blockedUsers.AsReadOnly());
		accessControl.RolloutPercentage.ShouldBe(rolloutPercentage);
	}

	[Fact]
	public void If_InternalConstructorWithNulls_ThenSetsEmptyLists()
	{
		// Act
		var accessControl = new FlagUserAccessControl(null, null, 0);

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(0);
		accessControl.BlockedUsers.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(0);
	}

	[Fact]
	public void If_InternalConstructorWithDefaults_ThenSetsDefaultValues()
	{
		// Act
		var accessControl = new FlagUserAccessControl();

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(0);
		accessControl.BlockedUsers.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(0);
	}
}

public class FlagUserAccessControl_Unrestricted
{
	[Fact]
	public void If_Unrestricted_ThenReturns100PercentRollout()
	{
		// Act
		var accessControl = FlagUserAccessControl.Unrestricted;

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(0);
		accessControl.BlockedUsers.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(100);
	}

	[Fact]
	public void If_UnrestrictedMultipleCalls_ThenReturnsSeparateInstances()
	{
		// Act
		var accessControl1 = FlagUserAccessControl.Unrestricted;
		var accessControl2 = FlagUserAccessControl.Unrestricted;

		// Assert
		accessControl1.ShouldNotBe(accessControl2);
		accessControl1.RolloutPercentage.ShouldBe(accessControl2.RolloutPercentage);
	}
}

public class FlagUserAccessControl_CreateAccessControl
{
	[Fact]
	public void If_ValidParameters_ThenCreatesAccessControl()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "user2" };
		var blockedUsers = new List<string> { "user3", "user4" };
		var rolloutPercentage = 50;

		// Act
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers, blockedUsers, rolloutPercentage);

		// Assert
		accessControl.AllowedUsers.ShouldBe(allowedUsers.AsReadOnly());
		accessControl.BlockedUsers.ShouldBe(blockedUsers.AsReadOnly());
		accessControl.RolloutPercentage.ShouldBe(rolloutPercentage);
	}

	[Fact]
	public void If_NullLists_ThenCreatesWithEmptyLists()
	{
		// Act
		var accessControl = FlagUserAccessControl.CreateAccessControl(null, null, 25);

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(0);
		accessControl.BlockedUsers.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(25);
	}

	[Fact]
	public void If_DefaultParameters_ThenCreatesWithDefaults()
	{
		// Act
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(0);
		accessControl.BlockedUsers.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(0);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(-10)]
	[InlineData(101)]
	[InlineData(150)]
	public void If_InvalidRolloutPercentage_ThenThrowsArgumentException(int invalidPercentage)
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			FlagUserAccessControl.CreateAccessControl(rolloutPercentage: invalidPercentage));
		exception.Message.ShouldBe("Rollout percentage must be between 0 and 100. (Parameter 'rolloutPercentage')");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(50)]
	[InlineData(100)]
	public void If_ValidRolloutPercentage_ThenCreatesSuccessfully(int validPercentage)
	{
		// Act
		var accessControl = Should.NotThrow(() =>
			FlagUserAccessControl.CreateAccessControl(rolloutPercentage: validPercentage));

		// Assert
		accessControl.RolloutPercentage.ShouldBe(validPercentage);
	}

	[Fact]
	public void If_ConflictingUsers_ThenThrowsArgumentException()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "user2", "USER3" };
		var blockedUsers = new List<string> { "user3", "user4" }; // user3 conflicts (case insensitive)

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			FlagUserAccessControl.CreateAccessControl(allowedUsers, blockedUsers));
		exception.Message.ShouldBe("Users cannot be in both allowed and blocked lists: USER3");
	}

	[Fact]
	public void If_MultipleConflicts_ThenIncludesAllInErrorMessage()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "USER2", "user3" };
		var blockedUsers = new List<string> { "User1", "user2", "user4" };

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			FlagUserAccessControl.CreateAccessControl(allowedUsers, blockedUsers));
		exception.Message.ShouldContain("user1");
		exception.Message.ShouldContain("USER2");
	}

	[Fact]
	public void If_EmptyAndWhitespaceUsers_ThenFiltersOut()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "", "   ", null!, "user2" };
		var blockedUsers = new List<string> { "user3", "", "   ", "user4" };

		// Act
		var accessControl = FlagUserAccessControl.CreateAccessControl(allowedUsers, blockedUsers);

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(2);
		accessControl.AllowedUsers.ShouldContain("user1");
		accessControl.AllowedUsers.ShouldContain("user2");
		accessControl.BlockedUsers.Count.ShouldBe(2);
		accessControl.BlockedUsers.ShouldContain("user3");
		accessControl.BlockedUsers.ShouldContain("user4");
	}

	[Fact]
	public void If_DuplicateUsers_ThenRemovesDuplicates()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "USER1", "user2", "user1" };
		var blockedUsers = new List<string> { "user3", "User3", "user4" };

		// Act
		var accessControl = FlagUserAccessControl.CreateAccessControl(allowedUsers, blockedUsers);

		// Assert
		accessControl.AllowedUsers.Count.ShouldBe(2);
		accessControl.AllowedUsers.ShouldContain("user1");
		accessControl.AllowedUsers.ShouldContain("user2");
		accessControl.BlockedUsers.Count.ShouldBe(2);
		accessControl.BlockedUsers.ShouldContain("user3");
		accessControl.BlockedUsers.ShouldContain("user4");
	}

	[Fact]
	public void If_UsersWithWhitespace_ThenTrimsWhitespace()
	{
		// Arrange
		var allowedUsers = new List<string> { "  user1  ", "\tuser2\t" };
		var blockedUsers = new List<string> { " user3 ", "user4   " };

		// Act
		var accessControl = FlagUserAccessControl.CreateAccessControl(allowedUsers, blockedUsers);

		// Assert
		accessControl.AllowedUsers.ShouldContain("user1");
		accessControl.AllowedUsers.ShouldContain("user2");
		accessControl.BlockedUsers.ShouldContain("user3");
		accessControl.BlockedUsers.ShouldContain("user4");
	}
}

public class FlagUserAccessControl_HasAccessRestrictions
{
	[Fact]
	public void If_NoRestrictions_ThenReturnsFalse()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			rolloutPercentage: 100);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeFalse();
	}

	[Fact]
	public void If_HasAllowedUsers_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["user1"],
			rolloutPercentage: 100);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeTrue();
	}

	[Fact]
	public void If_HasBlockedUsers_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: ["user1"],
			rolloutPercentage: 100);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(25)]
	[InlineData(50)]
	[InlineData(99)]
	public void If_RolloutPercentageLessThan100_ThenReturnsTrue(int percentage)
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			rolloutPercentage: percentage);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeTrue();
	}

	[Fact]
	public void If_Unrestricted_ThenReturnsFalse()
	{
		// Act & Assert
		FlagUserAccessControl.Unrestricted.HasAccessRestrictions().ShouldBeFalse();
	}
}

public class FlagUserAccessControl_EvaluateUserAccess
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t")]
	public void If_InvalidUserId_ThenReturnsDeniedWithReason(string? invalidUserId)
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess(invalidUserId!, flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Denied);
		reason.ShouldBe("User ID is required");
	}

	[Fact]
	public void If_UserExplicitlyBlocked_ThenReturnsDenied()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: ["blocked-user"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("blocked-user", flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Denied);
		reason.ShouldBe("User explicitly blocked");
	}

	[Fact]
	public void If_UserExplicitlyBlockedCaseInsensitive_ThenReturnsDenied()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: ["BLOCKED-USER"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("blocked-user", flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Denied);
		reason.ShouldBe("User explicitly blocked");
	}

	[Fact]
	public void If_UserExplicitlyAllowed_ThenReturnsAllowed()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["allowed-user"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("allowed-user", flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Allowed);
		reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public void If_UserExplicitlyAllowedCaseInsensitive_ThenReturnsAllowed()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["ALLOWED-USER"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("allowed-user", flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Allowed);
		reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public void If_UserBlockedTakesPrecedenceOverAllowed_ThenReturnsDenied()
	{
		// Arrange - Using internal constructor to bypass validation
		var accessControlWithConflict = new FlagUserAccessControl(
			allowedUsers: ["conflict-user"],
			blockedUsers: ["conflict-user"]);

		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControlWithConflict.EvaluateUserAccess("conflict-user", flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Denied);
		reason.ShouldBe("User explicitly blocked");
	}

	[Fact]
	public void If_ZeroRolloutPercentageAndNotExplicit_ThenReturnsDenied()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 0);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("any-user", flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Denied);
		reason.ShouldBe("Access restricted to all users");
	}

	[Fact]
	public void If_100PercentRolloutAndNotExplicit_ThenReturnsAllowed()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 100);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("any-user", flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Allowed);
		reason.ShouldBe("Access unrestricted to all users");
	}

	[Fact]
	public void If_UserIdWithWhitespace_ThenTrimsAndEvaluates()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["allowed-user"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateUserAccess("  allowed-user  ", flagKey);

		// Assert
		result.ShouldBe(UserAccessResult.Allowed);
		reason.ShouldBe("User explicitly allowed");
	}

	[Fact]
	public void If_PartialRolloutUserInRollout_ThenReturnsAllowed()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50);
		var flagKey = "consistent-flag-key";
		
		// Find a user that falls within the rollout by testing multiple user IDs
		string? userInRollout = null;
		for (int i = 0; i < 100; i++)
		{
			var testUser = $"test-user-{i}";
			var (result, reason) = accessControl.EvaluateUserAccess(testUser, flagKey);
			if (result == UserAccessResult.Allowed)
			{
				userInRollout = testUser;
				break;
			}
		}

		// Assert we found a user in rollout and verify the result
		userInRollout.ShouldNotBeNull();
		var (finalResult, finalReason) = accessControl.EvaluateUserAccess(userInRollout, flagKey);
		finalResult.ShouldBe(UserAccessResult.Allowed);
		finalReason.ShouldMatch(@"User in rollout: \d+% < 50%");
	}

	[Fact]
	public void If_PartialRolloutUserNotInRollout_ThenReturnsDenied()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50);
		var flagKey = "consistent-flag-key";
		
		// Find a user that falls outside the rollout by testing multiple user IDs
		string? userNotInRollout = null;
		for (int i = 0; i < 100; i++)
		{
			var testUser = $"test-user-{i}";
			var (result, reason) = accessControl.EvaluateUserAccess(testUser, flagKey);
			if (result == UserAccessResult.Denied && reason.Contains("not in rollout"))
			{
				userNotInRollout = testUser;
				break;
			}
		}

		// Assert we found a user not in rollout and verify the result
		userNotInRollout.ShouldNotBeNull();
		var (finalResult, finalReason) = accessControl.EvaluateUserAccess(userNotInRollout, flagKey);
		finalResult.ShouldBe(UserAccessResult.Denied);
		finalReason.ShouldMatch(@"User not in rollout: \d+% >= 50%");
	}

	[Fact]
	public void If_SameUserDifferentFlags_ThenMayHaveDifferentResults()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50);
		var userId = "consistent-user";

		// Act
		var (result1, reason1) = accessControl.EvaluateUserAccess(userId, "flag-one");
		var (result2, reason2) = accessControl.EvaluateUserAccess(userId, "flag-two");

		// Assert - Results may be different due to different flag keys in hash
		result1.ShouldBeOneOf(UserAccessResult.Allowed, UserAccessResult.Denied);
		result2.ShouldBeOneOf(UserAccessResult.Allowed, UserAccessResult.Denied);
		
		// Verify reasons match expected patterns
		if (result1 == UserAccessResult.Allowed)
			reason1.ShouldMatch(@"User in rollout: \d+% < 50%");
		else
			reason1.ShouldMatch(@"User not in rollout: \d+% >= 50%");
			
		if (result2 == UserAccessResult.Allowed)
			reason2.ShouldMatch(@"User in rollout: \d+% < 50%");
		else
			reason2.ShouldMatch(@"User not in rollout: \d+% >= 50%");
	}
}

public class FlagUserAccessControl_IsUserExplicitlyManaged
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_InvalidUserId_ThenReturnsFalse(string? invalidUserId)
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act & Assert
		accessControl.IsUserExplicitlyManaged(invalidUserId!).ShouldBeFalse();
	}

	[Fact]
	public void If_UserInAllowedList_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["allowed-user"]);

		// Act & Assert
		accessControl.IsUserExplicitlyManaged("allowed-user").ShouldBeTrue();
	}

	[Fact]
	public void If_UserInBlockedList_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: ["blocked-user"]);

		// Act & Assert
		accessControl.IsUserExplicitlyManaged("blocked-user").ShouldBeTrue();
	}

	[Fact]
	public void If_UserInBothLists_ThenReturnsTrue()
	{
		// Arrange - Using internal constructor to bypass validation
		var accessControl = new FlagUserAccessControl(
			allowedUsers: ["user1"],
			blockedUsers: ["user1"]);

		// Act & Assert
		accessControl.IsUserExplicitlyManaged("user1").ShouldBeTrue();
	}

	[Fact]
	public void If_UserNotInAnyList_ThenReturnsFalse()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["allowed-user"],
			blockedUsers: ["blocked-user"]);

		// Act & Assert
		accessControl.IsUserExplicitlyManaged("other-user").ShouldBeFalse();
	}

	[Fact]
	public void If_CaseInsensitiveMatch_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["ALLOWED-USER"]);

		// Act & Assert
		accessControl.IsUserExplicitlyManaged("allowed-user").ShouldBeTrue();
	}

	[Fact]
	public void If_UserIdWithWhitespace_ThenTrimsAndChecks()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["allowed-user"]);

		// Act & Assert
		accessControl.IsUserExplicitlyManaged("  allowed-user  ").ShouldBeTrue();
	}
}

public class FlagUserAccessControl_WithAllowedUser
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_InvalidUserId_ThenThrowsArgumentException(string? invalidUserId)
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			accessControl.WithAllowedUser(invalidUserId!));
		exception.Message.ShouldBe("User ID cannot be null or empty. (Parameter 'userId')");
	}

	[Fact]
	public void If_UserNotAlreadyAllowed_ThenAddsToAllowedList()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["existing-user"]);

		// Act
		var newAccessControl = accessControl.WithAllowedUser("new-user");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedUsers.Count.ShouldBe(2);
		newAccessControl.AllowedUsers.ShouldContain("existing-user");
		newAccessControl.AllowedUsers.ShouldContain("new-user");
	}

	[Fact]
	public void If_UserAlreadyAllowed_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["existing-user"]);

		// Act
		var newAccessControl = accessControl.WithAllowedUser("existing-user");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_UserAlreadyAllowedCaseInsensitive_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["EXISTING-USER"]);

		// Act
		var newAccessControl = accessControl.WithAllowedUser("existing-user");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_UserCurrentlyBlocked_ThenRemovesFromBlockedAndAddsToAllowed()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: new List<string> { "blocked-user", "other-user" });

		// Act
		var newAccessControl = accessControl.WithAllowedUser("blocked-user");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedUsers.Count.ShouldBe(1);
		newAccessControl.AllowedUsers.ShouldContain("blocked-user");
		newAccessControl.BlockedUsers.Count.ShouldBe(1);
		newAccessControl.BlockedUsers.ShouldContain("other-user");
		newAccessControl.BlockedUsers.ShouldNotContain("blocked-user");
	}

	[Fact]
	public void If_UserIdWithWhitespace_ThenTrimsBeforeProcessing()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act
		var newAccessControl = accessControl.WithAllowedUser("  new-user  ");

		// Assert
		newAccessControl.AllowedUsers.Count.ShouldBe(1);
		newAccessControl.AllowedUsers.ShouldContain("new-user");
	}

	[Fact]
	public void If_PreservesOtherProperties_ThenKeepsRolloutPercentage()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 75);

		// Act
		var newAccessControl = accessControl.WithAllowedUser("new-user");

		// Assert
		newAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class FlagUserAccessControl_WithBlockedUser
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_InvalidUserId_ThenThrowsArgumentException(string? invalidUserId)
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			accessControl.WithBlockedUser(invalidUserId!));
		exception.Message.ShouldBe("User ID cannot be null or empty. (Parameter 'userId')");
	}

	[Fact]
	public void If_UserNotAlreadyBlocked_ThenAddsToBlockedList()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: ["existing-user"]);

		// Act
		var newAccessControl = accessControl.WithBlockedUser("new-user");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.BlockedUsers.Count.ShouldBe(2);
		newAccessControl.BlockedUsers.ShouldContain("existing-user");
		newAccessControl.BlockedUsers.ShouldContain("new-user");
	}

	[Fact]
	public void If_UserAlreadyBlocked_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: ["existing-user"]);

		// Act
		var newAccessControl = accessControl.WithBlockedUser("existing-user");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_UserAlreadyBlockedCaseInsensitive_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: ["EXISTING-USER"]);

		// Act
		var newAccessControl = accessControl.WithBlockedUser("existing-user");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_UserCurrentlyAllowed_ThenRemovesFromAllowedAndAddsToBlocked()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: new List<string> { "allowed-user", "other-user" });

		// Act
		var newAccessControl = accessControl.WithBlockedUser("allowed-user");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.BlockedUsers.Count.ShouldBe(1);
		newAccessControl.BlockedUsers.ShouldContain("allowed-user");
		newAccessControl.AllowedUsers.Count.ShouldBe(1);
		newAccessControl.AllowedUsers.ShouldContain("other-user");
		newAccessControl.AllowedUsers.ShouldNotContain("allowed-user");
	}

	[Fact]
	public void If_UserIdWithWhitespace_ThenTrimsBeforeProcessing()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act
		var newAccessControl = accessControl.WithBlockedUser("  new-user  ");

		// Assert
		newAccessControl.BlockedUsers.Count.ShouldBe(1);
		newAccessControl.BlockedUsers.ShouldContain("new-user");
	}

	[Fact]
	public void If_PreservesOtherProperties_ThenKeepsRolloutPercentage()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 75);

		// Act
		var newAccessControl = accessControl.WithBlockedUser("new-user");

		// Assert
		newAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class FlagUserAccessControl_WithoutUser
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_InvalidUserId_ThenThrowsArgumentException(string? invalidUserId)
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			accessControl.WithoutUser(invalidUserId!));
		exception.Message.ShouldBe("User ID cannot be null or empty. (Parameter 'userId')");
	}

	[Fact]
	public void If_UserInAllowedList_ThenRemovesFromAllowed()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: new List<string> { "user1", "user2", "user3" });

		// Act
		var newAccessControl = accessControl.WithoutUser("user2");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedUsers.Count.ShouldBe(2);
		newAccessControl.AllowedUsers.ShouldContain("user1");
		newAccessControl.AllowedUsers.ShouldContain("user3");
		newAccessControl.AllowedUsers.ShouldNotContain("user2");
	}

	[Fact]
	public void If_UserInBlockedList_ThenRemovesFromBlocked()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			blockedUsers: new List<string> { "user1", "user2", "user3" });

		// Act
		var newAccessControl = accessControl.WithoutUser("user2");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.BlockedUsers.Count.ShouldBe(2);
		newAccessControl.BlockedUsers.ShouldContain("user1");
		newAccessControl.BlockedUsers.ShouldContain("user3");
		newAccessControl.BlockedUsers.ShouldNotContain("user2");
	}

	[Fact]
	public void If_UserInBothLists_ThenRemovesFromBoth()
	{
		// Arrange - Using internal constructor to bypass validation
		var accessControl = new FlagUserAccessControl(
			allowedUsers: ["user1", "user2"],
			blockedUsers: ["user2", "user3"]);

		// Act
		var newAccessControl = accessControl.WithoutUser("user2");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedUsers.Count.ShouldBe(1);
		newAccessControl.AllowedUsers.ShouldContain("user1");
		newAccessControl.BlockedUsers.Count.ShouldBe(1);
		newAccessControl.BlockedUsers.ShouldContain("user3");
	}

	[Fact]
	public void If_UserNotInAnyList_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["user1"],
			blockedUsers: ["user2"]);

		// Act
		var newAccessControl = accessControl.WithoutUser("user3");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_CaseInsensitiveMatch_ThenRemovesUser()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["USER1", "user2"]);

		// Act
		var newAccessControl = accessControl.WithoutUser("user1");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedUsers.Count.ShouldBe(1);
		newAccessControl.AllowedUsers.ShouldContain("user2");
		newAccessControl.AllowedUsers.ShouldNotContain("USER1");
	}

	[Fact]
	public void If_UserIdWithWhitespace_ThenTrimsBeforeProcessing()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["user1"]);

		// Act
		var newAccessControl = accessControl.WithoutUser("  user1  ");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedUsers.Count.ShouldBe(0);
	}

	[Fact]
	public void If_PreservesOtherProperties_ThenKeepsRolloutPercentage()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["user1"],
			rolloutPercentage: 75);

		// Act
		var newAccessControl = accessControl.WithoutUser("user1");

		// Assert
		newAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class FlagUserAccessControl_WithRolloutPercentage
{
	[Theory]
	[InlineData(-1)]
	[InlineData(-10)]
	[InlineData(101)]
	[InlineData(150)]
	public void If_InvalidPercentage_ThenThrowsArgumentException(int invalidPercentage)
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			accessControl.WithRolloutPercentage(invalidPercentage));
		exception.Message.ShouldBe("Rollout percentage must be between 0 and 100. (Parameter 'percentage')");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(25)]
	[InlineData(50)]
	[InlineData(75)]
	[InlineData(100)]
	public void If_ValidPercentage_ThenUpdatesRolloutPercentage(int validPercentage)
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 50);

		// Act
		var newAccessControl = accessControl.WithRolloutPercentage(validPercentage);

		// Assert
		if (validPercentage == 50)
		{
			newAccessControl.ShouldBe(accessControl);
		}
		else
		{
			newAccessControl.ShouldNotBe(accessControl);
			newAccessControl.RolloutPercentage.ShouldBe(validPercentage);
		}
	}

	[Fact]
	public void If_SamePercentage_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(rolloutPercentage: 75);

		// Act
		var newAccessControl = accessControl.WithRolloutPercentage(75);

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_PreservesOtherProperties_ThenKeepsUserLists()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "user2" };
		var blockedUsers = new List<string> { "user3", "user4" };
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers, blockedUsers, 25);

		// Act
		var newAccessControl = accessControl.WithRolloutPercentage(75);

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedUsers.ShouldBe(allowedUsers.AsReadOnly());
		newAccessControl.BlockedUsers.ShouldBe(blockedUsers.AsReadOnly());
		newAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class FlagUserAccessControl_FluentInterface
{
	[Fact]
	public void If_ChainedMethods_ThenWorksCorrectly()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act
		var result = accessControl
			.WithAllowedUser("user1")
			.WithAllowedUser("user2")
			.WithBlockedUser("user3")
			.WithRolloutPercentage(50);

		// Assert
		result.AllowedUsers.Count.ShouldBe(2);
		result.AllowedUsers.ShouldContain("user1");
		result.AllowedUsers.ShouldContain("user2");
		result.BlockedUsers.Count.ShouldBe(1);
		result.BlockedUsers.ShouldContain("user3");
		result.RolloutPercentage.ShouldBe(50);
	}

	[Fact]
	public void If_ChainedWithConflicts_ThenResolvesCorrectly()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl();

		// Act
		var result = accessControl
			.WithAllowedUser("user1")
			.WithBlockedUser("user1") // Should move from allowed to blocked
			.WithAllowedUser("user1"); // Should move back to allowed

		// Assert
		result.AllowedUsers.Count.ShouldBe(1);
		result.AllowedUsers.ShouldContain("user1");
		result.BlockedUsers.Count.ShouldBe(0);
	}

	[Fact]
	public void If_ChainedRemoval_ThenRemovesCorrectly()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["user1", "user2"],
			blockedUsers: ["user3"]);

		// Act
		var result = accessControl
			.WithoutUser("user1")
			.WithoutUser("user3");

		// Assert
		result.AllowedUsers.Count.ShouldBe(1);
		result.AllowedUsers.ShouldContain("user2");
		result.BlockedUsers.Count.ShouldBe(0);
	}
}

public class FlagUserAccessControl_ImmutabilityAndThreadSafety
{
	[Fact]
	public void If_ReadOnlyLists_ThenCannotBeModifiedExternally()
	{
		// Arrange
		var allowedUsers = new List<string> { "user1", "user2" };
		var accessControl = FlagUserAccessControl.CreateAccessControl(allowedUsers: allowedUsers);

		// Act - Modify original list
		allowedUsers.Add("user3");

		// Assert - Internal list should not be affected
		accessControl.AllowedUsers.Count.ShouldBe(2);
		accessControl.AllowedUsers.ShouldNotContain("user3");
	}

	[Fact]
	public void If_ModificationMethods_ThenReturnNewInstances()
	{
		// Arrange
		var original = FlagUserAccessControl.CreateAccessControl();

		// Act
		var withAllowed = original.WithAllowedUser("user1");
		var withBlocked = original.WithBlockedUser("user2");
		var withPercentage = original.WithRolloutPercentage(50);

		// Assert - All should be different instances
		withAllowed.ShouldNotBe(original);
		withBlocked.ShouldNotBe(original);
		withPercentage.ShouldNotBe(original);
		withAllowed.ShouldNotBe(withBlocked);
		withAllowed.ShouldNotBe(withPercentage);
		withBlocked.ShouldNotBe(withPercentage);
	}

	[Fact]
	public void If_NoChangeNeeded_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = FlagUserAccessControl.CreateAccessControl(
			allowedUsers: ["user1"],
			rolloutPercentage: 75);

		// Act
		var sameAllowed = accessControl.WithAllowedUser("user1");
		var samePercentage = accessControl.WithRolloutPercentage(75);
		var removingNonExistent = accessControl.WithoutUser("nonexistent");

		// Assert
		sameAllowed.ShouldBe(accessControl);
		samePercentage.ShouldBe(accessControl);
		removingNonExistent.ShouldBe(accessControl);
	}
}