using Propel.FeatureFlags.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Propel.FeatureFlags.Utilities;

/// <summary>
/// Provides default configuration settings for JSON serialization and deserialization.
/// </summary>
/// <remarks>This class defines a set of preconfigured <see cref="JsonSerializerOptions"/> tailored for common use
/// cases,  including camelCase property naming, case-insensitive property matching, and support for custom converters.
/// It is intended to simplify and standardize JSON handling across the application.</remarks>
public static class JsonDefaults
{
	public static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
			new TargetingRuleJsonConverter(),
			new EnumJsonConverter<TargetingOperator>(),
			new EnumJsonConverter<EvaluationMode>(),
			new EnumJsonConverter<DayOfWeek>(),
		}
	};
}

/// <summary>
/// Provides custom JSON serialization and deserialization for objects implementing the <see cref="ITargetingRule"/>
/// interface.
/// </summary>
/// <remarks>This converter supports polymorphic serialization and deserialization of targeting rules based on a
/// type discriminator or the structure of the JSON payload. It handles the <see cref="StringTargetingRule"/> and <see
/// cref="NumericTargetingRule"/> types.</remarks>
public class TargetingRuleJsonConverter : JsonConverter<ITargetingRule>
{
	private const string TypeDiscriminator = "$type";

	public override ITargetingRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException("Expected StartObject token");
		}

		using var document = JsonDocument.ParseValue(ref reader);
		var jsonObject = document.RootElement;

		// Check if there's a type discriminator
		if (jsonObject.TryGetProperty(TypeDiscriminator, out var typeProperty))
		{
			var typeName = typeProperty.GetString();
			return typeName switch
			{
				nameof(StringTargetingRule) => JsonSerializer.Deserialize<StringTargetingRule>(jsonObject.GetRawText(), options),
				nameof(NumericTargetingRule) => JsonSerializer.Deserialize<NumericTargetingRule>(jsonObject.GetRawText(), options),
				_ => throw new JsonException($"Unknown targeting rule type: {typeName}")
			};
		}

		// Fallback: Try to infer type from Values property (check both camelCase and PascalCase)
		JsonElement valuesProperty = default;
		bool hasValues = jsonObject.TryGetProperty("values", out valuesProperty) ||
						 jsonObject.TryGetProperty("Values", out valuesProperty);

		if (hasValues && valuesProperty.ValueKind == JsonValueKind.Array)
		{
			var firstValue = valuesProperty.EnumerateArray().FirstOrDefault();

			return firstValue.ValueKind switch
			{
				JsonValueKind.String => JsonSerializer.Deserialize<StringTargetingRule>(jsonObject.GetRawText(), options),
				JsonValueKind.Number => JsonSerializer.Deserialize<NumericTargetingRule>(jsonObject.GetRawText(), options),
				_ => throw new JsonException("Cannot determine targeting rule type from Values property")
			};
		}

		// Default to StringTargetingRule if we can't determine the type
		return JsonSerializer.Deserialize<StringTargetingRule>(jsonObject.GetRawText(), options);
	}

	public override void Write(Utf8JsonWriter writer, ITargetingRule value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();

		// Write type discriminator
		writer.WriteString(TypeDiscriminator, value.GetType().Name);

		// Write all properties using camelCase naming to match the JsonOptions policy
		switch (value)
		{
			case StringTargetingRule stringRule:
				WriteStringTargetingRule(writer, stringRule, options);
				break;
			case NumericTargetingRule numericRule:
				WriteNumericTargetingRule(writer, numericRule, options);
				break;
			default:
				throw new JsonException($"Unknown targeting rule type: {value.GetType().Name}");
		}

		writer.WriteEndObject();
	}

	private static void WriteStringTargetingRule(Utf8JsonWriter writer, StringTargetingRule rule, JsonSerializerOptions options)
	{
		// Use camelCase property names to match JsonNamingPolicy.CamelCase
		writer.WriteString("attribute", rule.Attribute);
		writer.WriteNumber("operator", (int)rule.Operator);
		writer.WriteString("variation", rule.Variation);

		writer.WritePropertyName("values");
		JsonSerializer.Serialize(writer, rule.Values, options);
	}

	private static void WriteNumericTargetingRule(Utf8JsonWriter writer, NumericTargetingRule rule, JsonSerializerOptions options)
	{
		// Use camelCase property names to match JsonNamingPolicy.CamelCase
		writer.WriteString("attribute", rule.Attribute);
		writer.WriteNumber("operator", (int)rule.Operator);
		writer.WriteString("variation", rule.Variation);

		writer.WritePropertyName("values");
		JsonSerializer.Serialize(writer, rule.Values, options);
	}
}

/// <summary>
/// Provides a custom JSON converter for serializing and deserializing enumeration values of type <typeparamref
/// name="T"/>.
/// </summary>
/// <remarks>This converter supports both string and numeric representations of enumeration values during
/// deserialization: - If the JSON token is a string, the converter attempts to parse the string as an enumeration name,
/// ignoring case. - If the JSON token is a number, the converter attempts to map the numeric value to a defined
/// enumeration value. During serialization, enumeration values are written as their numeric representation.</remarks>
/// <typeparam name="T">The enumeration type to be converted. Must be a value type that is an enumeration.</typeparam>
public class EnumJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.String)
		{
			var stringValue = reader.GetString();
			if (Enum.TryParse<T>(stringValue, ignoreCase: true, out var result))
			{
				return result;
			}
			throw new JsonException($"Unable to convert \"{stringValue}\" to {nameof(T)}.");
		}

		if (reader.TokenType == JsonTokenType.Number)
		{
			var intValue = reader.GetInt32();
			if (Enum.IsDefined(typeof(T), intValue))
			{
				return (T)Enum.ToObject(typeof(T), intValue); // (T)intValue;
			}
			throw new JsonException($"Unable to convert {intValue} to {nameof(T)}.");
		}

		throw new JsonException($"Unexpected token type {reader.TokenType} when parsing {nameof(T)}.");
	}

	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
	{
		//writer.WriteStringValue(value.ToString());
		writer.WriteNumberValue(Convert.ToInt32(value));
	}
}
