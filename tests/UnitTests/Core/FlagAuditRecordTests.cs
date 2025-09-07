using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagAuditRecord_Constructor
{
	[Fact]
	public void If_ValidParameters_ThenCreatesAuditRecord()
	{
		// Arrange
		var createdAt = DateTime.UtcNow.AddMinutes(-5);
		var modifiedAt = DateTime.UtcNow.AddMinutes(-1);
		var createdBy = "test-user";
		var modifiedBy = "modifier-user";

		// Act
		var auditRecord = new FlagAuditRecord(createdAt, createdBy, modifiedAt, modifiedBy);

		// Assert
		auditRecord.CreatedAt.ShouldBe(createdAt);
		auditRecord.ModifiedAt.ShouldBe(modifiedAt);
		auditRecord.CreatedBy.ShouldBe(createdBy);
		auditRecord.ModifiedBy.ShouldBe(modifiedBy);
	}

	[Fact]
	public void If_ModifiedAtBeforeCreatedAt_ThenThrowsArgumentException()
	{
		// Arrange
		var createdAt = DateTime.UtcNow;
		var modifiedAt = DateTime.UtcNow.AddMinutes(-5);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() => 
			new FlagAuditRecord(createdAt, "user", modifiedAt, "modifier"));
		exception.ParamName.ShouldBe("modifiedAt");
	}

	[Fact]
	public void If_CreatedByIsNullOrWhitespace_ThenSetsUnknown()
	{
		// Act
		var auditRecord = new FlagAuditRecord(DateTime.UtcNow, "   ");

		// Assert
		auditRecord.CreatedBy.ShouldBe("unknown");
	}
}

public class FlagAuditRecord_NewFlag
{
	[Fact]
	public void If_CreatedByProvided_ThenUsesProvidedCreator()
	{
		// Act
		var auditRecord = FlagAuditRecord.NewFlag("test-creator");

		// Assert
		auditRecord.CreatedBy.ShouldBe("test-creator");
		auditRecord.ModifiedAt.ShouldBeNull();
		auditRecord.ModifiedBy.ShouldBeNull();
		auditRecord.CreatedAt.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
	}
}