using Propel.FeatureFlags.Domain;

namespace FeatureFlags.UnitTests.Domain;

public class AccessControl_Validation
{
	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	public void Constructor_InvalidRolloutPercentage_ThrowsArgumentException(int invalidPercentage)
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new AccessControl(rolloutPercentage: invalidPercentage));
		exception.ParamName.ShouldBe("rolloutPercentage");
	}

	[Fact]
	public void Constructor_ConflictingTenants_ThrowsArgumentException()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "TENANT2" };
		var blockedTenants = new List<string> { "Tenant1", "tenant3" };

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new AccessControl(allowedTenants, blockedTenants));
	}

	[Fact]
	public void Constructor_FiltersEmptyTenants()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "", "   ", null!, "tenant2" };

		// Act
		var accessControl = new AccessControl(allowedTenants);

		// Assert
		accessControl.Allowed.Count.ShouldBe(2);
		accessControl.Allowed.ShouldContain("tenant1");
		accessControl.Allowed.ShouldContain("tenant2");
	}

	[Fact]
	public void Constructor_RemovesDuplicateTenants()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "TENANT1", "tenant2" };

		// Act
		var accessControl = new AccessControl(allowedTenants);

		// Assert
		accessControl.Allowed.Count.ShouldBe(2);
		accessControl.Allowed.ShouldContain("tenant1");
		accessControl.Allowed.ShouldContain("tenant2");
	}
}

public class AccessControl_Unrestricted
{
	[Fact]
	public void Unrestricted_Returns100PercentRollout()
	{
		// Act
		var accessControl = AccessControl.Unrestricted;

		// Assert
		accessControl.Allowed.Count.ShouldBe(0);
		accessControl.Allowed.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(100);
	}
}

public class AccessControl_HasAccessRestrictions
{
	[Fact]
	public void HasAccessRestrictions_NoRestrictions_ReturnsFalse()
	{
		// Arrange
		var accessControl = new AccessControl(rolloutPercentage: 100);

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
		var accessControl = new AccessControl(rolloutPercentage: percentage);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeTrue();
	}

	[Fact]
	public void HasAccessRestrictions_WithTenantLists_ReturnsTrue()
	{
		// Arrange
		var withAllowed = new AccessControl(allowed: ["tenant1"], rolloutPercentage: 100);
		var withBlocked = new AccessControl(blocked: ["tenant1"], rolloutPercentage: 100);

		// Act & Assert
		withAllowed.HasAccessRestrictions().ShouldBeTrue();
		withBlocked.HasAccessRestrictions().ShouldBeTrue();
	}
}

public class AccessControl_EvaluateTenantAccess
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void EvaluateTenantAccess_InvalidTenantId_ReturnsDenied(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new AccessControl();

		// Act
		var (result, reason) = accessControl.EvaluateAccess(invalidTenantId!, "test-flag");

		// Assert
		result.ShouldBe(AccessResult.Denied);
	}

	[Fact]
	public void EvaluateTenantAccess_ExplicitlyBlocked_ReturnsDenied()
	{
		// Arrange
		var accessControl = new AccessControl(blocked: ["blocked-tenant"], rolloutPercentage: 50);

		// Act
		var (result, reason) = accessControl.EvaluateAccess("BLOCKED-TENANT", "test-flag");

		// Assert
		result.ShouldBe(AccessResult.Denied);
	}

	[Fact]
	public void EvaluateTenantAccess_ExplicitlyAllowed_ReturnsAllowed()
	{
		// Arrange
		var accessControl = new AccessControl(allowed: ["allowed-tenant"], rolloutPercentage: 50);

		// Act
		var (result, reason) = accessControl.EvaluateAccess("ALLOWED-TENANT", "test-flag");

		// Assert
		result.ShouldBe(AccessResult.Allowed);
	}

	[Fact]
	public void EvaluateTenantAccess_ZeroRollout_ReturnsDenied()
	{
		// Arrange
		var accessControl = new AccessControl(rolloutPercentage: 0);

		// Act
		var (result, reason) = accessControl.EvaluateAccess("any-tenant", "test-flag");

		// Assert
		result.ShouldBe(AccessResult.Denied);
	}

	[Fact]
	public void EvaluateTenantAccess_FullRollout_ReturnsAllowed()
	{
		// Arrange
		var accessControl = new AccessControl(rolloutPercentage: 100);

		// Act
		var (result, reason) = accessControl.EvaluateAccess("any-tenant", "test-flag");

		// Assert
		result.ShouldBe(AccessResult.Allowed);
	}

	[Fact]
	public void EvaluateTenantAccess_PartialRollout_UsesConsistentHashing()
	{
		// Arrange
		var accessControl = new AccessControl(rolloutPercentage: 50);
		var tenantId = "test-tenant";
		var flagKey = "test-flag";

		// Act
		var (result1, _) = accessControl.EvaluateAccess(tenantId, flagKey);
		var (result2, _) = accessControl.EvaluateAccess(tenantId, flagKey);

		// Assert - Same tenant/flag should get consistent results
		result1.ShouldBe(result2);
	}

	[Fact]
	public void EvaluateTenantAccess_DifferentFlags_MayHaveDifferentResults()
	{
		// Arrange
		var accessControl = new AccessControl(rolloutPercentage: 50);
		var tenantId = "test-tenant";

		// Act
		var (result1, _) = accessControl.EvaluateAccess(tenantId, "flag-one");
		var (result2, _) = accessControl.EvaluateAccess(tenantId, "flag-two");

		// Assert - Different flags may produce different results due to hashing
		result1.ShouldBeOneOf(AccessResult.Allowed, AccessResult.Denied);
		result2.ShouldBeOneOf(AccessResult.Allowed, AccessResult.Denied);
	}
}

public class AccessControl_FluentInterface
{
	[Fact]
	public void WithAllowedTenant_AddsTenantToAllowedList()
	{
		// Arrange
		var accessControl = new AccessControl();

		// Act
		var result = accessControl.AllowAccessFor("new-tenant");

		// Assert
		result.ShouldNotBe(accessControl);
		result.Allowed.ShouldContain("new-tenant");
	}

	[Fact]
	public void WithAllowedTenant_AlreadyAllowed_ReturnsSameInstance()
	{
		// Arrange
		var accessControl = new AccessControl(allowed: ["existing-tenant"]);

		// Act
		var result = accessControl.AllowAccessFor("existing-tenant");

		// Assert
		result.ShouldBe(accessControl);
	}

	[Fact]
	public void WithBlockedTenant_MovesFromAllowedToBlocked()
	{
		// Arrange
		var accessControl = new AccessControl(allowed: ["tenant1", "tenant2"]);

		// Act
		var result = accessControl.BlockAccessFor("tenant1");

		// Assert
		result.Allowed.ShouldNotContain("tenant1");
		result.Blocked.ShouldContain("tenant1");
		result.Allowed.ShouldContain("tenant2");
	}

	[Fact]
	public void WithoutTenant_RemovesFromBothLists()
	{
		// Arrange
		var accessControl = new AccessControl(
			allowed: ["tenant1", "tenant2"],
			blocked: ["tenant3"]);

		// Act
		var result = accessControl.Remove("tenant1").Remove("tenant3");

		// Assert
		result.Allowed.ShouldBe(new[] { "tenant2" });
		result.Blocked.ShouldBeEmpty();
	}

	[Fact]
	public void WithRolloutPercentage_UpdatesPercentage()
	{
		// Arrange
		var accessControl = new AccessControl(rolloutPercentage: 50);

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
		var accessControl = new AccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.WithRolloutPercentage(invalidPercentage));
	}

	[Fact]
	public void FluentInterface_ChainedMethods_WorksCorrectly()
	{
		// Arrange
		var accessControl = new AccessControl();

		// Act
		var result = accessControl
			.AllowAccessFor("tenant1")
			.BlockAccessFor("tenant2")
			.WithRolloutPercentage(50);

		// Assert
		result.Allowed.ShouldBe(new[] { "tenant1" });
		result.Blocked.ShouldBe(new[] { "tenant2" });
		result.RolloutPercentage.ShouldBe(50);
	}

	[Fact]
	public void FluentInterface_MovingTenantBetweenLists_ResolvesCorrectly()
	{
		// Arrange
		var accessControl = new AccessControl();

		// Act
		var result = accessControl
			.AllowAccessFor("tenant1")
			.BlockAccessFor("tenant1") // Move to blocked
			.AllowAccessFor("tenant1"); // Move back to allowed

		// Assert
		result.Allowed.ShouldBe(new[] { "tenant1" });
		result.Blocked.ShouldBeEmpty();
	}
}

public class AccessControl_InvalidInputs
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithAllowedTenant_InvalidTenantId_ThrowsArgumentException(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new AccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.AllowAccessFor(invalidTenantId!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithBlockedTenant_InvalidTenantId_ThrowsArgumentException(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new AccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.BlockAccessFor(invalidTenantId!));
	}
}