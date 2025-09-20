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