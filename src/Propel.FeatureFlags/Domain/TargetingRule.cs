using System.Globalization;

namespace Propel.FeatureFlags.Domain;

public enum TargetingOperator
{
	Equals,
	NotEquals,
	Contains,
	NotContains,
	In,
	NotIn,
	GreaterThan,
	LessThan
}

/// <summary>
/// Represents a rule used to determine the variation to apply based on a specific attribute and operator.
/// </summary>
/// <remarks>A targeting rule consists of an attribute, an operator, and a variation. The rule is evaluated to
/// determine whether a specific variation should be applied based on the attribute's value and the operator's
/// logic.</remarks>
public interface ITargetingRule
{
	string Attribute { get; set; }
	TargetingOperator Operator { get; set; }
	string Variation { get; set; }
}

public interface ITargetingRule<T> : ITargetingRule
{
	List<T> Values { get; set; }
	bool EvaluateFor(T value);
}

/// <summary>
/// Represents a targeting rule for evaluating string-based conditions against a specified value.
/// </summary>
/// <remarks>This class is used to define a rule that evaluates whether a given string value satisfies a condition
/// based on the specified operator and a list of target values. The rule also specifies a variation that can be used to
/// represent the outcome of the evaluation.</remarks>
public class StringTargetingRule : ITargetingRule<string>
{
	public string Attribute { get; set; } = string.Empty;
	public TargetingOperator Operator { get; set; }
	public List<string> Values { get; set; } = [];
	public string Variation { get; set; } = String.Empty;

	public bool EvaluateFor(string value)
	{

		var result = Operator switch
		{
			TargetingOperator.Equals => Values.All(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)),
			TargetingOperator.In => Values.Contains(value, StringComparer.OrdinalIgnoreCase),
			TargetingOperator.Contains => Values.Any(v => value.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0),

			TargetingOperator.NotEquals => !Values.Contains(value, StringComparer.OrdinalIgnoreCase),
			TargetingOperator.NotIn => !Values.Contains(value, StringComparer.OrdinalIgnoreCase),
			TargetingOperator.NotContains => Values.All(v => value.IndexOf(v, StringComparison.OrdinalIgnoreCase) < 0),

			TargetingOperator.GreaterThan => Values.All(v => string.Compare(v, value, StringComparison.OrdinalIgnoreCase) < 0),
			TargetingOperator.LessThan => Values.All(v => string.Compare(v, value, StringComparison.OrdinalIgnoreCase) > 0),
			_ => false
		};

		return result;
	}

	public override string ToString()
	{
		var valuesStr = string.Join(", ", Values);
		return $"{Attribute} {Operator} [{valuesStr}] => {Variation}";
	}
}

/// <summary>
/// Represents a targeting rule that evaluates numeric values against a specified condition.
/// </summary>
/// <remarks>This class is used to define a rule for targeting based on numeric attributes.  The rule evaluates
/// whether a given numeric value satisfies the specified operator and matches the defined values.</remarks>
public class NumericTargetingRule : ITargetingRule<double>
{
	public string Attribute { get; set; } = string.Empty;
	public TargetingOperator Operator { get; set; }
	public List<double> Values { get; set; } = [];
	public string Variation { get; set; } = String.Empty;

	public bool EvaluateFor(double value)
	{

		var result = Operator switch
		{
			TargetingOperator.Equals or
			TargetingOperator.Contains or
			TargetingOperator.In => Values.Contains(value),

			TargetingOperator.NotEquals or
			TargetingOperator.NotContains or
			TargetingOperator.NotIn => !Values.Contains(value),

			TargetingOperator.GreaterThan => Values.All(v => v < value),
			TargetingOperator.LessThan => Values.All(v => v > value),
			_ => false
		};

		return result;
	}

	public override string ToString()
	{
		var valuesStr = string.Join(", ", Values);
		return $"{Attribute} {Operator} [{valuesStr}] => {Variation}";
	}
}

/// <summary>
/// Provides a factory for creating targeting rules based on the specified attribute, operator, values, and variation.
/// </summary>
/// <remarks>This factory method determines the appropriate type of targeting rule to create based on the data
/// type of the provided values. If all values can be parsed as numeric, a numeric targeting rule is created. Otherwise,
/// a string targeting rule is created.</remarks>
public class TargetingRuleFactory
{
	public static ITargetingRule CreateTargetingRule(string attribute, TargetingOperator op, List<string> values, string variation)
	{
		if (values.Count > 0 && values.All(v => double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _)))
		{
			var numericValues = values.Select(v => double.Parse(v, NumberStyles.Any, CultureInfo.InvariantCulture)).ToList();
			return CreateNumericTargetingRule(attribute, op, numericValues, variation);
		}

		return new StringTargetingRule
		{
			Attribute = attribute,
			Operator = op,
			Values = values,
			Variation = variation
		};
	}
	private static ITargetingRule CreateNumericTargetingRule(string attribute, TargetingOperator op, List<double> values, string variation)
	{
		return new NumericTargetingRule
		{
			Attribute = attribute,
			Operator = op,
			Values = values,
			Variation = variation
		};
	}
}