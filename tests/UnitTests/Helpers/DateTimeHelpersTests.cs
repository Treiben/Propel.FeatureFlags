using Propel.FeatureFlags.Helpers;
using Shouldly;

namespace FeatureFlags.UnitTests.Helpers;

public class DateTimeHelpers_NormalizeToUtc_DateTime
{
	private readonly DateTime _utcReplacement = new(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

	[Fact]
	public void When_PassedUtcDateTime_Should_ReturnSameUtcDateTime()
	{
		// Arrange
		var utcDateTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(utcDateTime, _utcReplacement);

		// Assert
		result.ShouldBe(utcDateTime);
		result.Kind.ShouldBe(DateTimeKind.Utc);
	}

	[Fact]
	public void When_PassedLocalDateTime_Should_ReturnUtcDateTime()
	{
		// Arrange
		var localDateTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Local);

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(localDateTime, _utcReplacement);

		// Assert
		result.Kind.ShouldBe(DateTimeKind.Utc);
		result.ShouldBe(localDateTime.ToUniversalTime());
	}

	[Fact]
	public void When_PassedMinValue_Should_ReturnUtcMinValue()
	{
		// Arrange
		var minValue = DateTime.MinValue;

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(minValue, _utcReplacement);

		// Assert
		result.Kind.ShouldBe(DateTimeKind.Utc);
		result.ShouldBe(DateTime.MinValue.ToUniversalTime());
	}

	[Fact]
	public void When_PassedMaxValue_Should_ReturnUtcMaxValue()
	{
		// Arrange
		var maxValue = DateTime.MaxValue;

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(maxValue, _utcReplacement);

		// Assert
		result.Kind.ShouldBe(DateTimeKind.Utc);
		result.ShouldBe(DateTime.MaxValue.ToUniversalTime());
	}

	[Fact]
	public void When_PassedNull_Should_ReturnReplacementValue()
	{
		// Arrange
		DateTime? nullValue = null;

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(nullValue, _utcReplacement);

		// Assert
		result.ShouldBe(_utcReplacement);
		result.Kind.ShouldBe(DateTimeKind.Utc);
	}
}

public class DateTimeHelpers_NormalizeToUtc_DateTimeOffset
{
	private readonly DateTime _utcReplacement = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

	[Fact]
	public void When_PassedUtcDateTimeOffset_Should_ReturnUtcDateTime()
	{
		// Arrange
		var utcOffset = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(utcOffset, _utcReplacement);

		// Assert
		result.ShouldNotBeNull();
		result.Value.Kind.ShouldBe(DateTimeKind.Utc);
		result.Value.ShouldBe(utcOffset.DateTime);
	}

	[Fact]
	public void When_PassedLocalDateTimeOffset_Should_ReturnUtcDateTime()
	{
		// Arrange
		var localOffset = new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.FromHours(-5));

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(localOffset, _utcReplacement);

		// Assert
		result.ShouldNotBeNull();
		result.Value.Kind.ShouldBe(DateTimeKind.Utc);
		result.Value.ShouldBe(localOffset.DateTime.ToUniversalTime());
	}

	[Fact]
	public void When_PassedMinValue_Should_ReturnUtcMinValue()
	{
		// Arrange
		var minValue = DateTimeOffset.MinValue;

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(minValue, _utcReplacement);

		// Assert
		result.ShouldNotBeNull();
		result.Value.Kind.ShouldBe(DateTimeKind.Utc);
		result.Value.ShouldBe(DateTime.MinValue.ToUniversalTime());
	}

	[Fact]
	public void When_PassedMaxValue_Should_ReturnUtcMaxValue()
	{
		// Arrange
		var maxValue = DateTimeOffset.MaxValue;

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(maxValue, _utcReplacement);

		// Assert
		result.ShouldNotBeNull();
		result.Value.Kind.ShouldBe(DateTimeKind.Utc);
		result.Value.ShouldBe(DateTime.MaxValue.ToUniversalTime());
	}

	[Fact]
	public void When_PassedNull_Should_ReturnReplacementValueAsUtc()
	{
		// Arrange
		DateTimeOffset? nullValue = null;
		var localReplacement = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Local);

		// Act
		var result = DateTimeHelpers.NormalizeToUtc(nullValue, localReplacement);

		// Assert
		result.ShouldNotBeNull();
		result.Value.Kind.ShouldBe(DateTimeKind.Utc);
		result.Value.ShouldBe(localReplacement.ToUniversalTime());
	}
}
