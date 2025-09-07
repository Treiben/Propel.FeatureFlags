using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagTenantAccessControl_Constructor
{
	[Fact]
	public void If_InternalConstructor_ThenSetsProperties()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "tenant2" };
		var blockedTenants = new List<string> { "tenant3", "tenant4" };
		var rolloutPercentage = 75;

		// Act
		var accessControl = new FlagTenantAccessControl(allowedTenants, blockedTenants, rolloutPercentage);

		// Assert
		accessControl.AllowedTenants.ShouldBe(allowedTenants.AsReadOnly());
		accessControl.BlockedTenants.ShouldBe(blockedTenants.AsReadOnly());
		accessControl.RolloutPercentage.ShouldBe(rolloutPercentage);
	}

	[Fact]
	public void If_InternalConstructorWithNulls_ThenSetsEmptyLists()
	{
		// Act
		var accessControl = new FlagTenantAccessControl(null, null, 0);

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(0);
		accessControl.BlockedTenants.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(0);
	}

	[Fact]
	public void If_InternalConstructorWithDefaults_ThenSetsDefaultValues()
	{
		// Act
		var accessControl = new FlagTenantAccessControl();

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(0);
		accessControl.BlockedTenants.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(0);
	}
}

public class FlagTenantAccessControl_Unrestricted
{
	[Fact]
	public void If_Unrestricted_ThenReturns100PercentRollout()
	{
		// Act
		var accessControl = FlagTenantAccessControl.Unrestricted;

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(0);
		accessControl.BlockedTenants.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(100);
	}

	[Fact]
	public void If_UnrestrictedMultipleCalls_ThenReturnsSeparateInstances()
	{
		// Act
		var accessControl1 = FlagTenantAccessControl.Unrestricted;
		var accessControl2 = FlagTenantAccessControl.Unrestricted;

		// Assert
		accessControl1.ShouldNotBe(accessControl2);
		accessControl1.RolloutPercentage.ShouldBe(accessControl2.RolloutPercentage);
	}
}

public class FlagTenantAccessControl_CreateAccessControl
{
	[Fact]
	public void If_ValidParameters_ThenCreatesAccessControl()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "tenant2" };
		var blockedTenants = new List<string> { "tenant3", "tenant4" };
		var rolloutPercentage = 50;

		// Act
		var accessControl = new FlagTenantAccessControl(
			allowedTenants, blockedTenants, rolloutPercentage);

		// Assert
		accessControl.AllowedTenants.ShouldBe(allowedTenants.AsReadOnly());
		accessControl.BlockedTenants.ShouldBe(blockedTenants.AsReadOnly());
		accessControl.RolloutPercentage.ShouldBe(rolloutPercentage);
	}

	[Fact]
	public void If_NullLists_ThenCreatesWithEmptyLists()
	{
		// Act
		var accessControl = new FlagTenantAccessControl(null, null, 25);

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(0);
		accessControl.BlockedTenants.Count.ShouldBe(0);
		accessControl.RolloutPercentage.ShouldBe(25);
	}

	[Fact]
	public void If_DefaultParameters_ThenCreatesWithDefaults()
	{
		// Act
		var accessControl = new FlagTenantAccessControl();

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(0);
		accessControl.BlockedTenants.Count.ShouldBe(0);
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
			new FlagTenantAccessControl(rolloutPercentage: invalidPercentage));
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
			new FlagTenantAccessControl(rolloutPercentage: validPercentage));

		// Assert
		accessControl.RolloutPercentage.ShouldBe(validPercentage);
	}

	[Fact]
	public void If_ConflictingTenants_ThenThrowsArgumentException()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "tenant2", "TENANT3" };
		var blockedTenants = new List<string> { "tenant3", "tenant4" }; // tenant3 conflicts (case insensitive)

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new FlagTenantAccessControl(allowedTenants, blockedTenants));
		exception.Message.ShouldBe("Tenants cannot be in both allowed and blocked lists: TENANT3");
	}

	[Fact]
	public void If_MultipleConflicts_ThenIncludesAllInErrorMessage()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "TENANT2", "tenant3" };
		var blockedTenants = new List<string> { "Tenant1", "tenant2", "tenant4" };

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new FlagTenantAccessControl(allowedTenants, blockedTenants));
		exception.Message.ShouldContain("tenant1");
		exception.Message.ShouldContain("TENANT2");
	}

	[Fact]
	public void If_EmptyAndWhitespaceTenants_ThenFiltersOut()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "", "   ", null!, "tenant2" };
		var blockedTenants = new List<string> { "tenant3", "", "   ", "tenant4" };

		// Act
		var accessControl = new FlagTenantAccessControl(allowedTenants, blockedTenants);

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(2);
		accessControl.AllowedTenants.ShouldContain("tenant1");
		accessControl.AllowedTenants.ShouldContain("tenant2");
		accessControl.BlockedTenants.Count.ShouldBe(2);
		accessControl.BlockedTenants.ShouldContain("tenant3");
		accessControl.BlockedTenants.ShouldContain("tenant4");
	}

	[Fact]
	public void If_DuplicateTenants_ThenRemovesDuplicates()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "TENANT1", "tenant2", "tenant1" };
		var blockedTenants = new List<string> { "tenant3", "Tenant3", "tenant4" };

		// Act
		var accessControl = new FlagTenantAccessControl(allowedTenants, blockedTenants);

		// Assert
		accessControl.AllowedTenants.Count.ShouldBe(2);
		accessControl.AllowedTenants.ShouldContain("tenant1");
		accessControl.AllowedTenants.ShouldContain("tenant2");
		accessControl.BlockedTenants.Count.ShouldBe(2);
		accessControl.BlockedTenants.ShouldContain("tenant3");
		accessControl.BlockedTenants.ShouldContain("tenant4");
	}

	[Fact]
	public void If_TenantsWithWhitespace_ThenTrimsWhitespace()
	{
		// Arrange
		var allowedTenants = new List<string> { "  tenant1  ", "\ttenant2\t" };
		var blockedTenants = new List<string> { " tenant3 ", "tenant4   " };

		// Act
		var accessControl = new FlagTenantAccessControl(allowedTenants, blockedTenants);

		// Assert
		accessControl.AllowedTenants.ShouldContain("tenant1");
		accessControl.AllowedTenants.ShouldContain("tenant2");
		accessControl.BlockedTenants.ShouldContain("tenant3");
		accessControl.BlockedTenants.ShouldContain("tenant4");
	}
}

public class FlagTenantAccessControl_HasAccessRestrictions
{
	[Fact]
	public void If_NoRestrictions_ThenReturnsFalse()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			rolloutPercentage: 100);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeFalse();
	}

	[Fact]
	public void If_HasAllowedTenants_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["tenant1"],
			rolloutPercentage: 100);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeTrue();
	}

	[Fact]
	public void If_HasBlockedTenants_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["tenant1"],
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
		var accessControl = new FlagTenantAccessControl(
			rolloutPercentage: percentage);

		// Act & Assert
		accessControl.HasAccessRestrictions().ShouldBeTrue();
	}

	[Fact]
	public void If_Unrestricted_ThenReturnsFalse()
	{
		// Act & Assert
		FlagTenantAccessControl.Unrestricted.HasAccessRestrictions().ShouldBeFalse();
	}
}

public class FlagTenantAccessControl_EvaluateTenantAccess
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t")]
	public void If_InvalidTenantId_ThenReturnsDeniedWithReason(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess(invalidTenantId!, flagKey);

		// Assert
		result.ShouldBe(TenantAccessResult.Denied);
		reason.ShouldBe("Tenant ID is required");
	}

	[Fact]
	public void If_TenantExplicitlyBlocked_ThenReturnsDenied()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["blocked-tenant"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("blocked-tenant", flagKey);

		// Assert
		result.ShouldBe(TenantAccessResult.Denied);
		reason.ShouldBe("Tenant explicitly blocked");
	}

	[Fact]
	public void If_TenantExplicitlyBlockedCaseInsensitive_ThenReturnsDenied()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["BLOCKED-TENANT"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("blocked-tenant", flagKey);

		// Assert
		result.ShouldBe(TenantAccessResult.Denied);
		reason.ShouldBe("Tenant explicitly blocked");
	}

	[Fact]
	public void If_TenantExplicitlyAllowed_ThenReturnsAllowed()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["allowed-tenant"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("allowed-tenant", flagKey);

		// Assert
		result.ShouldBe(TenantAccessResult.Allowed);
		reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public void If_TenantExplicitlyAllowedCaseInsensitive_ThenReturnsAllowed()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["ALLOWED-TENANT"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("allowed-tenant", flagKey);

		// Assert
		result.ShouldBe(TenantAccessResult.Allowed);
		reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public void If_ZeroRolloutPercentageAndNotExplicit_ThenReturnsDenied()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 0);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("any-tenant", flagKey);

		// Assert
		result.ShouldBe(TenantAccessResult.Denied);
		reason.ShouldBe("Access restricted to all tenants");
	}

	[Fact]
	public void If_100PercentRolloutAndNotExplicit_ThenReturnsAllowed()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 100);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("any-tenant", flagKey);

		// Assert
		result.ShouldBe(TenantAccessResult.Allowed);
		reason.ShouldBe("Access unrestricted to all tenants");
	}

	[Fact]
	public void If_TenantIdWithWhitespace_ThenTrimsAndEvaluates()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["allowed-tenant"]);
		var flagKey = "test-flag";

		// Act
		var (result, reason) = accessControl.EvaluateTenantAccess("  allowed-tenant  ", flagKey);

		// Assert
		result.ShouldBe(TenantAccessResult.Allowed);
		reason.ShouldBe("Tenant explicitly allowed");
	}

	[Fact]
	public void If_PartialRolloutTenantInRollout_ThenReturnsAllowed()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 50);
		var flagKey = "consistent-flag-key";
		
		// Find a tenant that falls within the rollout by testing multiple tenant IDs
		string? tenantInRollout = null;
		for (int i = 0; i < 100; i++)
		{
			var testTenant = $"test-tenant-{i}";
			var (result, _) = accessControl.EvaluateTenantAccess(testTenant, flagKey);
			if (result == TenantAccessResult.Allowed)
			{
				tenantInRollout = testTenant;
				break;
			}
		}

		// Assert we found a tenant in rollout and verify the result
		tenantInRollout.ShouldNotBeNull();
		var (finalResult, finalReason) = accessControl.EvaluateTenantAccess(tenantInRollout, flagKey);
		finalResult.ShouldBe(TenantAccessResult.Allowed);
		finalReason.ShouldMatch(@"Tenant in rollout: \d+% < 50%");
	}

	[Fact]
	public void If_PartialRolloutTenantNotInRollout_ThenReturnsDenied()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 50);
		var flagKey = "consistent-flag-key";
		
		// Find a tenant that falls outside the rollout by testing multiple tenant IDs
		string? tenantNotInRollout = null;
		for (int i = 0; i < 100; i++)
		{
			var testTenant = $"test-tenant-{i}";
			var (result, reason) = accessControl.EvaluateTenantAccess(testTenant, flagKey);
			if (result == TenantAccessResult.Denied && reason.Contains("not in rollout"))
			{
				tenantNotInRollout = testTenant;
				break;
			}
		}

		// Assert we found a tenant not in rollout and verify the result
		tenantNotInRollout.ShouldNotBeNull();
		var (finalResult, finalReason) = accessControl.EvaluateTenantAccess(tenantNotInRollout, flagKey);
		finalResult.ShouldBe(TenantAccessResult.Denied);
		finalReason.ShouldMatch(@"Tenant not in rollout: \d+% >= 50%");
	}

	[Fact]
	public void If_SameTenantDifferentFlags_ThenMayHaveDifferentResults()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 50);
		var tenantId = "consistent-tenant";

		// Act
		var (result1, reason1) = accessControl.EvaluateTenantAccess(tenantId, "flag-one");
		var (result2, reason2) = accessControl.EvaluateTenantAccess(tenantId, "flag-two");

		// Assert - Results may be different due to different flag keys in hash
		result1.ShouldBeOneOf(TenantAccessResult.Allowed, TenantAccessResult.Denied);
		result2.ShouldBeOneOf(TenantAccessResult.Allowed, TenantAccessResult.Denied);
		
		// Verify reasons match expected patterns
		if (result1 == TenantAccessResult.Allowed)
			reason1.ShouldMatch(@"Tenant in rollout: \d+% < 50%");
		else
			reason1.ShouldMatch(@"Tenant not in rollout: \d+% >= 50%");
			
		if (result2 == TenantAccessResult.Allowed)
			reason2.ShouldMatch(@"Tenant in rollout: \d+% < 50%");
		else
			reason2.ShouldMatch(@"Tenant not in rollout: \d+% >= 50%");
	}
}

public class FlagTenantAccessControl_IsTenantExplicitlyManaged
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_InvalidTenantId_ThenReturnsFalse(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act & Assert
		accessControl.IsTenantExplicitlyManaged(invalidTenantId!).ShouldBeFalse();
	}

	[Fact]
	public void If_TenantInAllowedList_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["allowed-tenant"]);

		// Act & Assert
		accessControl.IsTenantExplicitlyManaged("allowed-tenant").ShouldBeTrue();
	}

	[Fact]
	public void If_TenantInBlockedList_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["blocked-tenant"]);

		// Act & Assert
		accessControl.IsTenantExplicitlyManaged("blocked-tenant").ShouldBeTrue();
	}

	[Fact]
	public void If_TenantInBothLists_ThenThrowsException()
	{
		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
				new FlagTenantAccessControl(
			allowedTenants: ["tenant1"],
			blockedTenants: ["tenant1"]));
	}

	[Fact]
	public void If_TenantNotInAnyList_ThenReturnsFalse()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["allowed-tenant"],
			blockedTenants: ["blocked-tenant"]);

		// Act & Assert
		accessControl.IsTenantExplicitlyManaged("other-tenant").ShouldBeFalse();
	}

	[Fact]
	public void If_CaseInsensitiveMatch_ThenReturnsTrue()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["ALLOWED-TENANT"]);

		// Act & Assert
		accessControl.IsTenantExplicitlyManaged("allowed-tenant").ShouldBeTrue();
	}

	[Fact]
	public void If_TenantIdWithWhitespace_ThenTrimsAndChecks()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["allowed-tenant"]);

		// Act & Assert
		accessControl.IsTenantExplicitlyManaged("  allowed-tenant  ").ShouldBeTrue();
	}
}

public class FlagTenantAccessControl_WithAllowedTenant
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_InvalidTenantId_ThenThrowsArgumentException(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			accessControl.WithAllowedTenant(invalidTenantId!));
		exception.Message.ShouldBe("Tenant ID cannot be null or empty. (Parameter 'tenantId')");
	}

	[Fact]
	public void If_TenantNotAlreadyAllowed_ThenAddsToAllowedList()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["existing-tenant"]);

		// Act
		var newAccessControl = accessControl.WithAllowedTenant("new-tenant");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedTenants.Count.ShouldBe(2);
		newAccessControl.AllowedTenants.ShouldContain("existing-tenant");
		newAccessControl.AllowedTenants.ShouldContain("new-tenant");
	}

	[Fact]
	public void If_TenantAlreadyAllowed_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["existing-tenant"]);

		// Act
		var newAccessControl = accessControl.WithAllowedTenant("existing-tenant");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_TenantAlreadyAllowedCaseInsensitive_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["EXISTING-TENANT"]);

		// Act
		var newAccessControl = accessControl.WithAllowedTenant("existing-tenant");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_TenantCurrentlyBlocked_ThenRemovesFromBlockedAndAddsToAllowed()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["blocked-tenant", "other-tenant"]);

		// Act
		var newAccessControl = accessControl.WithAllowedTenant("blocked-tenant");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedTenants.Count.ShouldBe(1);
		newAccessControl.AllowedTenants.ShouldContain("blocked-tenant");
		newAccessControl.BlockedTenants.Count.ShouldBe(1);
		newAccessControl.BlockedTenants.ShouldContain("other-tenant");
		newAccessControl.BlockedTenants.ShouldNotContain("blocked-tenant");
	}

	[Fact]
	public void If_TenantIdWithWhitespace_ThenTrimsBeforeProcessing()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act
		var newAccessControl = accessControl.WithAllowedTenant("  new-tenant  ");

		// Assert
		newAccessControl.AllowedTenants.Count.ShouldBe(1);
		newAccessControl.AllowedTenants.ShouldContain("new-tenant");
	}

	[Fact]
	public void If_PreservesOtherProperties_ThenKeepsRolloutPercentage()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 75);

		// Act
		var newAccessControl = accessControl.WithAllowedTenant("new-tenant");

		// Assert
		newAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class FlagTenantAccessControl_WithBlockedTenant
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_InvalidTenantId_ThenThrowsArgumentException(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			accessControl.WithBlockedTenant(invalidTenantId!));
		exception.Message.ShouldBe("Tenant ID cannot be null or empty. (Parameter 'tenantId')");
	}

	[Fact]
	public void If_TenantNotAlreadyBlocked_ThenAddsToBlockedList()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["existing-tenant"]);

		// Act
		var newAccessControl = accessControl.WithBlockedTenant("new-tenant");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.BlockedTenants.Count.ShouldBe(2);
		newAccessControl.BlockedTenants.ShouldContain("existing-tenant");
		newAccessControl.BlockedTenants.ShouldContain("new-tenant");
	}

	[Fact]
	public void If_TenantAlreadyBlocked_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["existing-tenant"]);

		// Act
		var newAccessControl = accessControl.WithBlockedTenant("existing-tenant");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_TenantAlreadyBlockedCaseInsensitive_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["EXISTING-TENANT"]);

		// Act
		var newAccessControl = accessControl.WithBlockedTenant("existing-tenant");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_TenantCurrentlyAllowed_ThenRemovesFromAllowedAndAddsToBlocked()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["allowed-tenant", "other-tenant"]);

		// Act
		var newAccessControl = accessControl.WithBlockedTenant("allowed-tenant");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.BlockedTenants.Count.ShouldBe(1);
		newAccessControl.BlockedTenants.ShouldContain("allowed-tenant");
		newAccessControl.AllowedTenants.Count.ShouldBe(1);
		newAccessControl.AllowedTenants.ShouldContain("other-tenant");
		newAccessControl.AllowedTenants.ShouldNotContain("allowed-tenant");
	}

	[Fact]
	public void If_TenantIdWithWhitespace_ThenTrimsBeforeProcessing()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act
		var newAccessControl = accessControl.WithBlockedTenant("  new-tenant  ");

		// Assert
		newAccessControl.BlockedTenants.Count.ShouldBe(1);
		newAccessControl.BlockedTenants.ShouldContain("new-tenant");
	}

	[Fact]
	public void If_PreservesOtherProperties_ThenKeepsRolloutPercentage()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 75);

		// Act
		var newAccessControl = accessControl.WithBlockedTenant("new-tenant");

		// Assert
		newAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class FlagTenantAccessControl_WithoutTenant
{
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void If_InvalidTenantId_ThenThrowsArgumentException(string? invalidTenantId)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			accessControl.WithoutTenant(invalidTenantId!));
		exception.Message.ShouldBe("Tenant ID cannot be null or empty. (Parameter 'tenantId')");
	}

	[Fact]
	public void If_TenantInAllowedList_ThenRemovesFromAllowed()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["tenant1", "tenant2", "tenant3"]);

		// Act
		var newAccessControl = accessControl.WithoutTenant("tenant2");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedTenants.Count.ShouldBe(2);
		newAccessControl.AllowedTenants.ShouldContain("tenant1");
		newAccessControl.AllowedTenants.ShouldContain("tenant3");
		newAccessControl.AllowedTenants.ShouldNotContain("tenant2");
	}

	[Fact]
	public void If_TenantInBlockedList_ThenRemovesFromBlocked()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			blockedTenants: ["tenant1", "tenant2", "tenant3"]);

		// Act
		var newAccessControl = accessControl.WithoutTenant("tenant2");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.BlockedTenants.Count.ShouldBe(2);
		newAccessControl.BlockedTenants.ShouldContain("tenant1");
		newAccessControl.BlockedTenants.ShouldContain("tenant3");
		newAccessControl.BlockedTenants.ShouldNotContain("tenant2");
	}

	[Fact]
	public void If_TenantNotInAnyList_ThenReturnsSameInstance()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["tenant1"],
			blockedTenants: ["tenant2"]);

		// Act
		var newAccessControl = accessControl.WithoutTenant("tenant3");

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_CaseInsensitiveMatch_ThenRemovesTenant()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["TENANT1", "tenant2"]);

		// Act
		var newAccessControl = accessControl.WithoutTenant("tenant1");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedTenants.Count.ShouldBe(1);
		newAccessControl.AllowedTenants.ShouldContain("tenant2");
		newAccessControl.AllowedTenants.ShouldNotContain("TENANT1");
	}

	[Fact]
	public void If_TenantIdWithWhitespace_ThenTrimsBeforeProcessing()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["tenant1"]);

		// Act
		var newAccessControl = accessControl.WithoutTenant("  tenant1  ");

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedTenants.Count.ShouldBe(0);
	}

	[Fact]
	public void If_PreservesOtherProperties_ThenKeepsRolloutPercentage()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["tenant1"],
			rolloutPercentage: 75);

		// Act
		var newAccessControl = accessControl.WithoutTenant("tenant1");

		// Assert
		newAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class FlagTenantAccessControl_WithRolloutPercentage
{
	[Theory]
	[InlineData(-1)]
	[InlineData(-10)]
	[InlineData(101)]
	[InlineData(150)]
	public void If_InvalidPercentage_ThenThrowsArgumentException(int invalidPercentage)
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

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
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 50);

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
		var accessControl = new FlagTenantAccessControl(rolloutPercentage: 75);

		// Act
		var newAccessControl = accessControl.WithRolloutPercentage(75);

		// Assert
		newAccessControl.ShouldBe(accessControl);
	}

	[Fact]
	public void If_PreservesOtherProperties_ThenKeepsTenantLists()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "tenant2" };
		var blockedTenants = new List<string> { "tenant3", "tenant4" };
		var accessControl = new FlagTenantAccessControl(
			allowedTenants, blockedTenants, 25);

		// Act
		var newAccessControl = accessControl.WithRolloutPercentage(75);

		// Assert
		newAccessControl.ShouldNotBe(accessControl);
		newAccessControl.AllowedTenants.ShouldBe(allowedTenants.AsReadOnly());
		newAccessControl.BlockedTenants.ShouldBe(blockedTenants.AsReadOnly());
		newAccessControl.RolloutPercentage.ShouldBe(75);
	}
}

public class FlagTenantAccessControl_FluentInterface
{
	[Fact]
	public void If_ChainedMethods_ThenWorksCorrectly()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act
		var result = accessControl
			.WithAllowedTenant("tenant1")
			.WithAllowedTenant("tenant2")
			.WithBlockedTenant("tenant3")
			.WithRolloutPercentage(50);

		// Assert
		result.AllowedTenants.Count.ShouldBe(2);
		result.AllowedTenants.ShouldContain("tenant1");
		result.AllowedTenants.ShouldContain("tenant2");
		result.BlockedTenants.Count.ShouldBe(1);
		result.BlockedTenants.ShouldContain("tenant3");
		result.RolloutPercentage.ShouldBe(50);
	}

	[Fact]
	public void If_ChainedWithConflicts_ThenResolvesCorrectly()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl();

		// Act
		var result = accessControl
			.WithAllowedTenant("tenant1")
			.WithBlockedTenant("tenant1") // Should move from allowed to blocked
			.WithAllowedTenant("tenant1"); // Should move back to allowed

		// Assert
		result.AllowedTenants.Count.ShouldBe(1);
		result.AllowedTenants.ShouldContain("tenant1");
		result.BlockedTenants.Count.ShouldBe(0);
	}

	[Fact]
	public void If_ChainedRemoval_ThenRemovesCorrectly()
	{
		// Arrange
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["tenant1", "tenant2"],
			blockedTenants: ["tenant3"]);

		// Act
		var result = accessControl
			.WithoutTenant("tenant1")
			.WithoutTenant("tenant3");

		// Assert
		result.AllowedTenants.Count.ShouldBe(1);
		result.AllowedTenants.ShouldContain("tenant2");
		result.BlockedTenants.Count.ShouldBe(0);
	}
}

public class FlagTenantAccessControl_ImmutabilityAndThreadSafety
{
	[Fact]
	public void If_ReadOnlyLists_ThenCannotBeModifiedExternally()
	{
		// Arrange
		var allowedTenants = new List<string> { "tenant1", "tenant2" };
		var accessControl = new FlagTenantAccessControl(allowedTenants: allowedTenants);

		// Act - Modify original list
		allowedTenants.Add("tenant3");

		// Assert - Internal list should not be affected
		accessControl.AllowedTenants.Count.ShouldBe(2);
		accessControl.AllowedTenants.ShouldNotContain("tenant3");
	}

	[Fact]
	public void If_ModificationMethods_ThenReturnNewInstances()
	{
		// Arrange
		var original = new FlagTenantAccessControl();

		// Act
		var withAllowed = original.WithAllowedTenant("tenant1");
		var withBlocked = original.WithBlockedTenant("tenant2");
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
		var accessControl = new FlagTenantAccessControl(
			allowedTenants: ["tenant1"],
			rolloutPercentage: 75);

		// Act
		var sameAllowed = accessControl.WithAllowedTenant("tenant1");
		var samePercentage = accessControl.WithRolloutPercentage(75);
		var removingNonExistent = accessControl.WithoutTenant("nonexistent");

		// Assert
		sameAllowed.ShouldBe(accessControl);
		samePercentage.ShouldBe(accessControl);
		removingNonExistent.ShouldBe(accessControl);
	}
}