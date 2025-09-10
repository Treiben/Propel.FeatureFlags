using Propel.FeatureFlags.Core;
using Xunit;

namespace Propel.FeatureFlags.Tests.Core;

public class StringTargetingRuleTests
{
	[Theory]
	[InlineData(TargetingOperator.Equals, "user123", new[] { "user123", "user456" }, true)]
	[InlineData(TargetingOperator.Equals, "USER123", new[] { "user123" }, true)] // Case insensitive
	[InlineData(TargetingOperator.Equals, "user789", new[] { "user123", "user456" }, false)]
	[InlineData(TargetingOperator.NotEquals, "user789", new[] { "user123", "user456" }, true)]
	[InlineData(TargetingOperator.NotEquals, "user123", new[] { "user123", "user456" }, false)]
	public void EvaluateFor_EqualsAndNotEquals_ReturnsExpectedResult(TargetingOperator op, string testValue, string[] ruleValues, bool expected)
	{
		// Arrange
		var rule = new StringTargetingRule
		{
			Operator = op,
			Values = ruleValues.ToList()
		};

		// Act
		var result = rule.EvaluateFor(testValue);

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(TargetingOperator.Contains, "user123@example.com", new[] { "@example.com", "admin" }, true)]
	[InlineData(TargetingOperator.Contains, "USER@EXAMPLE.COM", new[] { "@example.com" }, true)] // Case insensitive
	[InlineData(TargetingOperator.Contains, "user@gmail.com", new[] { "@example.com", "admin" }, false)]
	[InlineData(TargetingOperator.NotContains, "user@gmail.com", new[] { "@example.com", "admin" }, true)]
	[InlineData(TargetingOperator.NotContains, "admin@example.com", new[] { "@example.com", "admin" }, false)]
	public void EvaluateFor_ContainsAndNotContains_ReturnsExpectedResult(TargetingOperator op, string testValue, string[] ruleValues, bool expected)
	{
		// Arrange
		var rule = new StringTargetingRule
		{
			Operator = op,
			Values = ruleValues.ToList()
		};

		// Act
		var result = rule.EvaluateFor(testValue);

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(TargetingOperator.GreaterThan, "charlie", new[] { "alpha", "beta" }, true)]
	[InlineData(TargetingOperator.GreaterThan, "alpha", new[] { "beta", "charlie" }, false)]
	[InlineData(TargetingOperator.LessThan, "alpha", new[] { "beta", "charlie" }, true)]
	[InlineData(TargetingOperator.LessThan, "delta", new[] { "beta", "charlie" }, false)]
	public void EvaluateFor_GreaterThanAndLessThan_ReturnsExpectedResult(TargetingOperator op, string testValue, string[] ruleValues, bool expected)
	{
		// Arrange
		var rule = new StringTargetingRule
		{
			Operator = op,
			Values = ruleValues.ToList()
		};

		// Act
		var result = rule.EvaluateFor(testValue);

		// Assert
		Assert.Equal(expected, result);
	}
}

public class NumericTargetingRuleTests
{
	[Theory]
	[InlineData(TargetingOperator.Equals, 25.0, new[] { 25.0, 30.0 }, true)]
	[InlineData(TargetingOperator.Equals, 35.0, new[] { 25.0, 30.0 }, false)]
	[InlineData(TargetingOperator.NotEquals, 35.0, new[] { 25.0, 30.0 }, true)]
	[InlineData(TargetingOperator.NotEquals, 25.0, new[] { 25.0, 30.0 }, false)]
	public void EvaluateFor_EqualsAndNotEquals_ReturnsExpectedResult(TargetingOperator op, double testValue, double[] ruleValues, bool expected)
	{
		// Arrange
		var rule = new NumericTargetingRule
		{
			Operator = op,
			Values = ruleValues.ToList()
		};

		// Act
		var result = rule.EvaluateFor(testValue);

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(TargetingOperator.GreaterThan, 25.0, new[] { 18.0, 21.0 }, true)] // 25 > all values
	[InlineData(TargetingOperator.GreaterThan, 15.0, new[] { 18.0, 21.0 }, false)] // 15 not > all values
	[InlineData(TargetingOperator.LessThan, 15.0, new[] { 18.0, 21.0 }, true)] // 15 < all values
	[InlineData(TargetingOperator.LessThan, 25.0, new[] { 18.0, 21.0 }, false)] // 25 not < all values
	public void EvaluateFor_GreaterThanAndLessThan_ReturnsExpectedResult(TargetingOperator op, double testValue, double[] ruleValues, bool expected)
	{
		// Arrange
		var rule = new NumericTargetingRule
		{
			Operator = op,
			Values = ruleValues.ToList()
		};

		// Act
		var result = rule.EvaluateFor(testValue);

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(TargetingOperator.In, 25.0, new[] { 25.0, 30.0, 35.0 }, true)]
	[InlineData(TargetingOperator.Contains, 25.0, new[] { 25.0, 30.0, 35.0 }, true)] // Equivalent to In for numeric
	[InlineData(TargetingOperator.In, 40.0, new[] { 25.0, 30.0, 35.0 }, false)]
	[InlineData(TargetingOperator.NotIn, 40.0, new[] { 25.0, 30.0, 35.0 }, true)]
	[InlineData(TargetingOperator.NotContains, 40.0, new[] { 25.0, 30.0, 35.0 }, true)] // Equivalent to NotIn for numeric
	[InlineData(TargetingOperator.NotIn, 25.0, new[] { 25.0, 30.0, 35.0 }, false)]
	public void EvaluateFor_InAndNotIn_ReturnsExpectedResult(TargetingOperator op, double testValue, double[] ruleValues, bool expected)
	{
		// Arrange
		var rule = new NumericTargetingRule
		{
			Operator = op,
			Values = ruleValues.ToList()
		};

		// Act
		var result = rule.EvaluateFor(testValue);

		// Assert
		Assert.Equal(expected, result);
	}

	[Fact]
	public void EvaluateFor_UnsupportedOperator_ReturnsFalse()
	{
		// Arrange
		var rule = new NumericTargetingRule
		{
			Operator = (TargetingOperator)999, // Invalid operator
			Values = [25.0, 30.0]
		};

		// Act
		var result = rule.EvaluateFor(35.0);

		// Assert
		Assert.False(result);
	}
}