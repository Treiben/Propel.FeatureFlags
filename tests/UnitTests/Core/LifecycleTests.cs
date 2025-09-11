using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class Lifecycle_Constructor
{
	[Fact]
	public void If_PermanentFlag_ThenSetsMaxDateAndPermanent()
	{
		// Act
		var lifecycle = new Lifecycle(DateTime.UtcNow.AddDays(10), isPermanent: true);

		// Assert
		lifecycle.IsPermanent.ShouldBeTrue();
		lifecycle.ExpirationDate.ShouldBe(DateTime.MaxValue.ToUniversalTime());
	}

	[Fact]
	public void If_NullExpirationDate_ThenSetsDefault30Days()
	{
		// Act
		var lifecycle = new Lifecycle(null, isPermanent: false);

		// Assert
		lifecycle.IsPermanent.ShouldBeFalse();
		lifecycle.ExpirationDate.ShouldNotBeNull();
		lifecycle.ExpirationDate!.Value.ShouldBeInRange(DateTime.UtcNow.AddDays(29), DateTime.UtcNow.AddDays(31));
	}

	[Fact]
	public void If_ValidExpirationDate_ThenConvertsToUtc()
	{
		// Arrange
		var localDateTime = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Local);

		// Act
		var lifecycle = new Lifecycle(localDateTime, isPermanent: false);

		// Assert
		lifecycle.ExpirationDate.ShouldBe(localDateTime.ToUniversalTime());
		lifecycle.IsPermanent.ShouldBeFalse();
	}
}

public class Lifecycle_DefaultLifecycle
{
	[Fact]
	public void ThenReturns30DayExpirationNonPermanent()
	{
		// Act
		var lifecycle = Lifecycle.DefaultLifecycle;

		// Assert
		lifecycle.IsPermanent.ShouldBeFalse();
		lifecycle.ExpirationDate.ShouldNotBeNull();
		lifecycle.ExpirationDate!.Value.ShouldBeInRange(DateTime.UtcNow.AddDays(29), DateTime.UtcNow.AddDays(31));
	}
}