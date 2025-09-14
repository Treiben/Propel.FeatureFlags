using Propel.FeatureFlags.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Propel.FeatureFlags.Helpers;

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
