using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagAuditRecord_Constructor
{
	[Fact]
	public void If_ValidParameters_ThenCreatesAuditRecord()
	{
		// Arrange - local time
		var createdAt = DateTime.Now.AddMinutes(-5);
		var createdBy = "test-user";

		// Act
		var created = new Audit(timestamp: createdAt, actor: createdBy);

		// Assert
		created.Timestamp.ShouldBe(createdAt.ToUniversalTime());
		created.Actor.ShouldBe(createdBy);

		// Arrange and Act - UTC time
		created = new Audit(timestamp: createdAt.ToUniversalTime(), actor: createdBy);
		// Assert
		created.Timestamp.ShouldBe(createdAt.ToUniversalTime());

		// Arrange and Act - FlagCreated static method
		created = Audit.FlagCreated();
		// Assert
		created.Timestamp.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
		created.Actor.ShouldBe("system");

		// Arrange and Assert - expected to throw exception because timestamp is in the future
		Should.Throw<ArgumentException>(() => new Audit(timestamp: DateTime.Now.AddDays(1), actor: " another-user "));

		
	}
}

