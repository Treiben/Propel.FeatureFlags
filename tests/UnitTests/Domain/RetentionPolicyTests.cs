using Propel.FeatureFlags.Domain;

namespace FeatureFlags.UnitTests.Domain;

public class RetentionPolicy_Constructor
{
	[Fact]
	public void If_PermanentFlag_ThenSetsMaxDateAndPermanent()
	{
		// Act
		var retention = new RetentionPolicy(
			expirationDate:DateTime.UtcNow.AddDays(10), 
			applicationName: "TestApp",
			isPermanent: true);

		// Assert
		retention.IsPermanent.ShouldBeTrue();
		retention.ExpirationDate.ShouldBe(DateTime.MaxValue.ToUniversalTime());
	}

	[Fact]
	public void If_ValidLocalExpirationDate_ThenConvertsToUtc()
	{
		// Arrange
		var localDateTime = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Local);

		// Act
		var retention = new RetentionPolicy(expirationDate: localDateTime, applicationName: "TestApp", isPermanent: false);

		// Assert
		retention.ExpirationDate.ShouldBe(localDateTime.ToUniversalTime());
		retention.IsPermanent.ShouldBeFalse();
	}

	[Fact]
	public void If_ValidUtcExpirationDate_ThenDoNotConvert()
	{
		// Arrange
		var utcDateTime = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);

		// Act
		var retention = new RetentionPolicy(expirationDate: utcDateTime, applicationName: "TestApp", isPermanent: false);

		// Assert
		retention.ExpirationDate.ShouldBe(utcDateTime);
		retention.IsPermanent.ShouldBeFalse();
	}
}