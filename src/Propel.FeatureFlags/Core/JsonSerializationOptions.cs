using System.Text.Json;
using System.Text.Json.Serialization;

namespace Propel.FeatureFlags.Core;

public static class JsonDefaults
{
	public static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
			new ITargetingRuleJsonConverter(),
		}
	};
}

public class ITargetingRuleJsonConverter : JsonConverter<ITargetingRule>
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

		// Fallback: Try to infer type from Values property
		if (jsonObject.TryGetProperty("Values", out var valuesProperty) && valuesProperty.ValueKind == JsonValueKind.Array)
		{
			var firstValue = valuesProperty.EnumerateArray().FirstOrDefault();

			return firstValue.ValueKind switch
			{
				JsonValueKind.String => JsonSerializer.Deserialize<StringTargetingRule>(jsonObject.GetRawText(), options),
				JsonValueKind.Number => JsonSerializer.Deserialize<NumericTargetingRule>(jsonObject.GetRawText(), options),
				_ => throw new JsonException("Cannot determine targeting rule type from Values property")
			};
		}

		// Try to infer from property names if Values is missing or empty
		if (jsonObject.TryGetProperty("values", out var valuesPropertyLower) && valuesPropertyLower.ValueKind == JsonValueKind.Array)
		{
			var firstValue = valuesPropertyLower.EnumerateArray().FirstOrDefault();
			return firstValue.ValueKind switch
			{
				JsonValueKind.String => JsonSerializer.Deserialize<StringTargetingRule>(jsonObject.GetRawText(), options),
				JsonValueKind.Number => JsonSerializer.Deserialize<NumericTargetingRule>(jsonObject.GetRawText(), options),
				_ => throw new JsonException("Cannot determine targeting rule type from values property")
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

		// Write all properties of the concrete type
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
		writer.WriteString("Attribute", rule.Attribute);
		writer.WriteNumber("Operator", (int)rule.Operator);
		writer.WriteString("Variation", rule.Variation);

		writer.WritePropertyName("Values");
		JsonSerializer.Serialize(writer, rule.Values, options);
	}

	private static void WriteNumericTargetingRule(Utf8JsonWriter writer, NumericTargetingRule rule, JsonSerializerOptions options)
	{
		writer.WriteString("Attribute", rule.Attribute);
		writer.WriteNumber("Operator", (int)rule.Operator);
		writer.WriteString("Variation", rule.Variation);

		writer.WritePropertyName("Values");
		JsonSerializer.Serialize(writer, rule.Values, options);
	}
}
