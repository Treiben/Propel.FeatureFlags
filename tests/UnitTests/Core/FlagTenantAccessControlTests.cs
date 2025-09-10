using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagTenantAccessControl_Validation
{
	[Theory]
	[InlineData(-1)]
	[InlineData(101)]
	public void Constructor_InvalidRolloutPercentage_ThrowsArgumentException(int invalidPercentage)
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new FlagTenantAccessControl(rolloutPercentage: invalidPercentage));
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
			new FlagTenantAccessControl(allowedTenants, blockedTenants));
	}

	[Fact]
	public void Constructor_FiltersEmptyTenants()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "", "   ", null!, "tenant2" };

		// Act
		var accessControl = new FlagTenantAccessControl(allowedTenants);

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(2);
		accessControl.AllowedTenants.ShouldContain("tenant1");
		accessControl.AllowedTenants.ShouldContain("tenant2");
	}

	[Fact]
	public void Constructor_RemovesDuplicateTenants()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "TENANT1", "tenant2" };

		// Act
		var accessControl = new FlagTenantAccessControl(allowedTenants);

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(2);
		accessControl.AllowedTenants.ShouldContain("tenant1");
		accessControl.AllowedTenants.ShouldContain("tenant2");
	}
}

public class FlagTenantAccessControl_Unrestricted
{
	[Fact]
	public void Unrestricted_Returns100PercentRollout()
	{
		// Act
		var accessControl = FlagTenantAccessControl.Unrestricted;

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(0);
		accessControl.BlockedTenants.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(100);
	}
}

public class FlagTenantAccessControl_HasAccessRestrictions
{
	[Fact]
	public void HasAccessRestrictions_NoRestrictions_ReturnsFalse()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 100);

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
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: percentage);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeTrue();
	}

	[Fact]
	public void HasAccessRestrictions_WithTenantLists_ReturnsTrue()
	{
		// Arrange
		var withAllowed = new FlagTenantAccessControl(allowedTenants: ["tenant1"], rolloutPercentage: 100);
		var withBlocked = new FlagTenantAccessControl(blockedTenants: ["tenant1"], rolloutPercentage: 100);

		// Act & Assert
		withAllowed.HasAccessRestrictions().ShouldBeTrue();
		withBlocked.HasAccessRestrictions().ShouldBeTrue();
	}
}

public class FlagTenantAccessControl_EvaluateTenantAccess
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void EvaluateTenantAccess_InvalidTenantId_ReturnsDenied(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess(invalidTenantId!, "test-flag");

		// Assert
		result.ShouldBe(TenantAccessResult.Denied);
		reason.ShouldBe("Tenant ID is required");
	}

	[Fact]
	public void EvaluateTenantAccess_ExplicitlyBlocked_ReturnsDenied()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(blockedTenants: ["blocked-tenant"]);

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("BLOCKED-TENANT", "test-flag");

		// Assert
		result.ShouldBe(TenantAccessResult.Denied);
		reason.ShouldBe("Tenant explicitly blocked");
	}

	[Fact]
	public void EvaluateTenantAccess_ExplicitlyAllowed_ReturnsAllowed()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(allowedTenants: ["allowed-tenant"]);

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("ALLOWED-TENANT", "test-flag");

		// Assert
		result.ShouldBe(TenantAccessResult.Allowed);
		reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public void EvaluateTenantAccess_ZeroRollout_ReturnsDenied()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 0);

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("any-tenant", "test-flag");

		// Assert
		result.ShouldBe(TenantAccessResult.Denied);
		reason.ShouldBe("Access restricted to all tenants");
	}

	[Fact]
	public void EvaluateTenantAccess_FullRollout_ReturnsAllowed()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 100);

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("any-tenant", "test-flag");

		// Assert
		result.ShouldBe(TenantAccessResult.Allowed);
		reason.ShouldBe("Access unrestricted to all tenants");
	}

	[Fact]
	public void EvaluateTenantAccess_PartialRollout_UsesConsistentHashing()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 50);
		var tenantId = "test-tenant";
		var flagKey = "test-flag";

		// Act
		var (result1, _) = accessControl.EvaluateTenantAccess(tenantId, flagKey);
		var (result2, _) = accessControl.EvaluateTenantAccess(tenantId, flagKey);

		// Assert - Same tenant/flag should get consistent results
		result1.ShouldBe(result2);
	}

	[Fact]
	public void EvaluateTenantAccess_DifferentFlags_MayHaveDifferentResults()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 50);
		var tenantId = "test-tenant";

		// Act
		var (result1, _) = accessControl.EvaluateTenantAccess(tenantId, "flag-one");
		var (result2, _) = accessControl.EvaluateTenantAccess(tenantId, "flag-two");

		// Assert - Different flags may produce different results due to hashing
		result1.ShouldBeOneOf(TenantAccessResult.Allowed, TenantAccessResult.Denied);
		result2.ShouldBeOneOf(TenantAccessResult.Allowed, TenantAccessResult.Denied);
	}
}

public class FlagTenantAccessControl_FluentInterface
{
	[Fact]
	public void WithAllowedTenant_AddsTenantToAllowedList()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act
		var result = accessControl.WithAllowedTenant("new-tenant");

		// Assert
		result.ShouldNotBe(accessControl);
		result.AllowedTenants.ShouldContain("new-tenant");
	}

	[Fact]
	public void WithAllowedTenant_AlreadyAllowed_ReturnsSameInstance()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(allowedTenants: ["existing-tenant"]);

		// Act
		var result = accessControl.WithAllowedTenant("existing-tenant");

		// Assert
		result.ShouldBe(accessControl);
	}

	[Fact]
	public void WithBlockedTenant_MovesFromAllowedToBlocked()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(allowedTenants: ["tenant1", "tenant2"]);

		// Act
		var result = accessControl.WithBlockedTenant("tenant1");

		// Assert
		result.AllowedTenants.ShouldNotContain("tenant1");
		result.BlockedTenants.ShouldContain("tenant1");
		result.AllowedTenants.ShouldContain("tenant2");
	}

	[Fact]
	public void WithoutTenant_RemovesFromBothLists()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["tenant1", "tenant2"],
			blockedTenants: ["tenant3"]);

		// Act
		var result = accessControl.WithoutTenant("tenant1").WithoutTenant("tenant3");

		// Assert
		result.AllowedTenants.ShouldBe(new[] { "tenant2" });
		result.BlockedTenants.ShouldBeEmpty();
	}

	[Fact]
	public void WithRolloutPercentage_UpdatesPercentage()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 50);

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
		var accessControl = new FlagTenantAccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.WithRolloutPercentage(invalidPercentage));
	}

	[Fact]
	public void FluentInterface_ChainedMethods_WorksCorrectly()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act
		var result = accessControl
			.WithAllowedTenant("tenant1")
			.WithBlockedTenant("tenant2")
			.WithRolloutPercentage(50);

		// Assert
		result.AllowedTenants.ShouldBe(new[] { "tenant1" });
		result.BlockedTenants.ShouldBe(new[] { "tenant2" });
		result.RolloutPercentage.ShouldBe(50);
	}

	[Fact]
	public void FluentInterface_MovingTenantBetweenLists_ResolvesCorrectly()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act
		var result = accessControl
			.WithAllowedTenant("tenant1")
			.WithBlockedTenant("tenant1") // Move to blocked
			.WithAllowedTenant("tenant1"); // Move back to allowed

		// Assert
		result.AllowedTenants.ShouldBe(new[] { "tenant1" });
		result.BlockedTenants.ShouldBeEmpty();
	}
}

public class FlagTenantAccessControl_InvalidInputs
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithAllowedTenant_InvalidTenantId_ThrowsArgumentException(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.WithAllowedTenant(invalidTenantId!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithBlockedTenant_InvalidTenantId_ThrowsArgumentException(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act & Assert
		Should.Throw<ArgumentException>(() => accessControl.WithBlockedTenant(invalidTenantId!));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void IsTenantExplicitlyManaged_InvalidTenantId_ReturnsFalse(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act & Assert
		accessControl.IsTenantExplicitlyManaged(invalidTenantId!).ShouldBeFalse();
	}
}