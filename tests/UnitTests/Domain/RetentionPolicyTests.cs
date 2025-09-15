using Propel.FeatureFlags.Domain;

namespace FeatureFlags.UnitTests.Domain;

public class Lifecycle_Constructor
{
	[Fact]
	public void If_PermanentFlag_ThenSetsMaxDateAndPermanent()
	{
		// Act
		var lifecycle = new RetentionPolicy(
			expirationDate:DateTime.UtcNow.AddDays(10), 
			applicationName: "TestApp",
			isPermanent: true);

		// Assert
		lifecycle.IsPermanent.ShouldBeTrue();
		lifecycle.ExpirationDate.ShouldBe(DateTime.MaxValue.ToUniversalTime());
	}

	[Fact]
	public void If_ValidLocalExpirationDate_ThenConvertsToUtc()
	{
		// Arrange
		var localDateTime = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Local);

		// Act
		var lifecycle = new RetentionPolicy(expirationDate: localDateTime, applicationName: "TestApp", isPermanent: false);

		// Assert
		lifecycle.ExpirationDate.ShouldBe(localDateTime.ToUniversalTime());
		lifecycle.IsPermanent.ShouldBeFalse();
	}

	[Fact]
	public void If_ValidUtcExpirationDate_ThenDoNotConvert()
	{
		// Arrange
		var utcDateTime = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);

		// Act
		var lifecycle = new RetentionPolicy(expirationDate: utcDateTime, applicationName: "TestApp", isPermanent: false);

		// Assert
		lifecycle.ExpirationDate.ShouldBe(utcDateTime);
		lifecycle.IsPermanent.ShouldBeFalse();
	}
}