using Propel.FeatureFlags.Domain;

namespace FeatureFlags.UnitTests.Domain;

public class TargetingRuleFactory_CreaterTargetingRule
{
    [Fact]
    public void CreaterTargetingRule_AllNumericValues_CreatesNumericRule()
    {
        // Arrange
        var values = new List<string> { "1.5", "2.0", "10" };

        // Act
        var rule = TargetingRuleFactory.CreateTargetingRule("age", TargetingOperator.GreaterThan, values, "premium");

        // Assert
        rule.ShouldBeOfType<NumericTargetingRule>();
        rule.Attribute.ShouldBe("age");
        rule.Operator.ShouldBe(TargetingOperator.GreaterThan);
        rule.Variation.ShouldBe("premium");
        
        var numericRule = (NumericTargetingRule)rule;
        numericRule.Values.ShouldBe([1.5, 2.0, 10]);
    }

    [Fact]
    public void CreaterTargetingRule_MixedValues_CreatesStringRule()
    {
        // Arrange
        var values = new List<string> { "1.5", "abc", "10" };

        // Act
        var rule = TargetingRuleFactory.CreateTargetingRule("category", TargetingOperator.In, values, "special");

        // Assert
        rule.ShouldBeOfType<StringTargetingRule>();
        rule.Attribute.ShouldBe("category");
        rule.Operator.ShouldBe(TargetingOperator.In);
        rule.Variation.ShouldBe("special");
        
        var stringRule = (StringTargetingRule)rule;
        stringRule.Values.ShouldBe(values);
    }

    [Fact]
    public void CreaterTargetingRule_AllStringValues_CreatesStringRule()
    {
        // Arrange
        var values = new List<string> { "premium", "basic", "enterprise" };

        // Act
        var rule = TargetingRuleFactory.CreateTargetingRule("plan", TargetingOperator.Equals, values, "on");

        // Assert
        rule.ShouldBeOfType<StringTargetingRule>();
        var stringRule = (StringTargetingRule)rule;
        stringRule.Values.ShouldBe(values);
    }

    [Fact]
    public void CreaterTargetingRule_EmptyValues_CreatesStringRule()
    {
        // Arrange
        var values = new List<string>();

        // Act
        var rule = TargetingRuleFactory.CreateTargetingRule("test", TargetingOperator.Equals, values, "default");

        // Assert
        rule.ShouldBeOfType<StringTargetingRule>();
        var stringRule = (StringTargetingRule)rule;
        stringRule.Values.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("0", "NumericTargetingRule")]
    [InlineData("-5.5", "NumericTargetingRule")]
    [InlineData("1.23E+10", "NumericTargetingRule")]
    [InlineData("Infinity", "StringTargetingRule")]
    [InlineData("1.5.2", "StringTargetingRule")]
    [InlineData("", "StringTargetingRule")]
    [InlineData(" ", "StringTargetingRule")]
    public void CreaterTargetingRule_EdgeCaseValues_CreatesCorrectRuleType(string value, string expectedRuleType)
    {
        // Arrange
        var values = new List<string> { value };

        // Act
        var rule = TargetingRuleFactory.CreateTargetingRule("test", TargetingOperator.Equals, values, "on");

        // Assert
        if (expectedRuleType == "NumericTargetingRule")
        {
            rule.ShouldBeOfType<NumericTargetingRule>();
        }
        else
        {
            rule.ShouldBeOfType<StringTargetingRule>();
        }
    }

    [Fact]
    public void CreaterTargetingRule_IntegerValues_CreatesNumericRule()
    {
        // Arrange
        var values = new List<string> { "1", "2", "100" };

        // Act
        var rule = TargetingRuleFactory.CreateTargetingRule("count", TargetingOperator.LessThan, values, "limited");

        // Assert
        rule.ShouldBeOfType<NumericTargetingRule>();
        var numericRule = (NumericTargetingRule)rule;
        numericRule.Values.ShouldBe([1, 2, 100]);
    }

    [Fact]
    public void CreaterTargetingRule_NegativeNumbers_CreatesNumericRule()
    {
        // Arrange
        var values = new List<string> { "-1.5", "-10", "0" };

        // Act
        var rule = TargetingRuleFactory.CreateTargetingRule("balance", TargetingOperator.GreaterThan, values, "overdrawn");

        // Assert
        rule.ShouldBeOfType<NumericTargetingRule>();
        var numericRule = (NumericTargetingRule)rule;
        numericRule.Values.ShouldBe([-1.5, -10, 0]);
    }
}