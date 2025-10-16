using Propel.FeatureFlags.Domain;

namespace UnitTests.Domain;

public class AccessControlTests
{
    [Fact]
    public void EvaluateAccess_ShouldDenyAccess_WhenIdIsExplicitlyBlocked()
    {
        // Arrange
        var accessControl = new AccessControl(
            allowed: ["user1", "user2"],
            blocked: ["blocked-user"],
            rolloutPercentage: 100);

        // Act
        var (result, reason) = accessControl.EvaluateAccess("blocked-user", "test-flag");

        // Assert
        result.ShouldBe(AccessResult.Denied);
        reason.ShouldBe("Access is blocked");
    }

    [Fact]
    public void EvaluateAccess_ShouldAllowAccess_WhenIdIsExplicitlyAllowed()
    {
        // Arrange
        var accessControl = new AccessControl(
            allowed: ["user1", "user2"],
            blocked: [],
            rolloutPercentage: 0);

        // Act
        var (result, reason) = accessControl.EvaluateAccess("user1", "test-flag");

        // Assert
        result.ShouldBe(AccessResult.Allowed);
        reason.ShouldBe("Access is allowed");
    }

    [Theory]
    [InlineData(0, AccessResult.Denied, "Access restricted to all")]
    [InlineData(100, AccessResult.Allowed, "Access unrestricted to all")]
    public void EvaluateAccess_ShouldRespectRolloutPercentageBoundaries(int percentage, AccessResult expectedResult, string expectedReason)
    {
        // Arrange
        var accessControl = new AccessControl(rolloutPercentage: percentage);

        // Act
        var (result, reason) = accessControl.EvaluateAccess("random-user", "test-flag");

        // Assert
        result.ShouldBe(expectedResult);
        reason.ShouldBe(expectedReason);
    }

    [Fact]
    public void AllowAccessFor_ShouldRemoveIdFromBlockedList_WhenIdWasPreviouslyBlocked()
    {
        // Arrange
        var accessControl = new AccessControl(
            blocked: ["user1"],
            rolloutPercentage: 50);

        // Act
        var updated = accessControl.AllowAccessFor("user1");
        var (result, _) = updated.EvaluateAccess("user1", "test-flag");

        // Assert
        updated.Allowed.ShouldContain("user1");
        updated.Blocked.ShouldNotContain("user1");
        result.ShouldBe(AccessResult.Allowed);
    }

    [Fact]
    public void BlockAccessFor_ShouldRemoveIdFromAllowedList_WhenIdWasPreviouslyAllowed()
    {
        // Arrange
        var accessControl = new AccessControl(
            allowed: ["user1"],
            rolloutPercentage: 100);

        // Act
        var updated = accessControl.BlockAccessFor("user1");
        var (result, _) = updated.EvaluateAccess("user1", "test-flag");

        // Assert
        updated.Blocked.ShouldContain("user1");
        updated.Allowed.ShouldNotContain("user1");
        result.ShouldBe(AccessResult.Denied);
    }

    [Fact]
    public void EvaluateAccess_ShouldAllowNonBlockedUser_WhenOnlyBlockListExists()
    {
        // Arrange - EvaluateWithExplicitRules: blocked list only (no allow list)
        var accessControl = new AccessControl(
            allowed: null,
            blocked: ["blocked-user1", "blocked-user2"],
            rolloutPercentage: 0);

        // Act
        var (result, _) = accessControl.EvaluateAccess("regular-user", "test-flag");

        // Assert
        result.ShouldBe(AccessResult.Allowed);
    }

    [Fact]
    public void EvaluateAccess_ShouldDenyUnlistedUser_WhenOnlyAllowListExists()
    {
        // Arrange - EvaluateWithExplicitRules: allowed list only (no block list)
        var accessControl = new AccessControl(
            allowed: ["user1", "user2"],
            blocked: null,
            rolloutPercentage: 100);

        // Act
        var (result, _) = accessControl.EvaluateAccess("unlisted-user", "test-flag");

        // Assert
        result.ShouldBe(AccessResult.Denied);
    }

    [Fact]
    public void EvaluateAccess_ShouldUsePercentageRollout_WhenNoExplicitRulesExist()
    {
        // Arrange - EvaluateWithRolloutOnly: no allow/block lists, only percentage
        var accessControl = new AccessControl(
            allowed: null,
            blocked: null,
            rolloutPercentage: 50);

        // Act - Test with a user that will fall into the rollout based on hash
        var (result1, reason1) = accessControl.EvaluateAccess("test-user-in-rollout", "feature-flag");
        var (_, _) = accessControl.EvaluateAccess("test-user-not-in-rollout", "feature-flag");

        // Assert - At least one should be in rollout and one should be out
        // The exact result depends on hash, but we verify the reason format
        if (result1 == AccessResult.Allowed)
        {
            reason1.ShouldContain("Id is in rollout:");
            reason1.ShouldContain($"< {accessControl.RolloutPercentage}%");
        }
        else
        {
            reason1.ShouldContain("Id is not in rollout:");
            reason1.ShouldContain($">= {accessControl.RolloutPercentage}%");
        }
    }

    [Fact]
    public void EvaluateAccess_ShouldPrioritizeBlockedOverPercentage_WhenBothExist()
    {
        // Arrange - EvaluateWithExplicitRules: blocked user should be denied regardless of rollout
        var accessControl = new AccessControl(
            allowed: null,
            blocked: ["blocked-user"],
            rolloutPercentage: 100); // 100% rollout, but user is blocked

        // Act
        var (result, reason) = accessControl.EvaluateAccess("blocked-user", "test-flag");

        // Assert
        result.ShouldBe(AccessResult.Denied);
        reason.ShouldBe("Access is blocked");
    }

    [Fact]
    public void EvaluateAccess_ShouldConsistentlyEvaluateSameUserForSameFlag()
    {
        // Arrange - EvaluateWithRolloutOnly: verify hash-based rollout is deterministic
        var accessControl = new AccessControl(rolloutPercentage: 50);
        var userId = "consistent-user";
        var flagKey = "consistent-flag";

        // Act - Evaluate multiple times
        var (result1, reason1) = accessControl.EvaluateAccess(userId, flagKey);
        var (result2, reason2) = accessControl.EvaluateAccess(userId, flagKey);
        var (result3, reason3) = accessControl.EvaluateAccess(userId, flagKey);

        // Assert - All results should be identical
        result2.ShouldBe(result1);
        result3.ShouldBe(result1);
        reason2.ShouldBe(reason1);
        reason3.ShouldBe(reason1);
    }

    [Fact]
    public void EvaluateAccess_ShouldProduceDifferentResultsForDifferentFlags()
    {
        // Arrange - EvaluateWithRolloutOnly: same user, different flags should hash differently
        var accessControl = new AccessControl(rolloutPercentage: 50);
        var userId = "test-user";

        // Act - Evaluate same user with different flag keys
        var (result1, _) = accessControl.EvaluateAccess(userId, "flag-a");
        var (result2, _) = accessControl.EvaluateAccess(userId, "flag-b");
        var (result3, _) = accessControl.EvaluateAccess(userId, "flag-c");
        var (result4, _) = accessControl.EvaluateAccess(userId, "flag-d");
        var (result5, _) = accessControl.EvaluateAccess(userId, "flag-e");

        // Assert - With 50% rollout and 5 different flags, statistically we should see variation
        // Not all results should be the same (this would be extremely unlikely)
        var results = new[] { result1, result2, result3, result4, result5 };
        var distinctResults = results.Distinct().Count();
        
        // At least 2 different outcomes expected (not all allowed or all denied)
        distinctResults.ShouldBeGreaterThanOrEqualTo(2);
    }
}