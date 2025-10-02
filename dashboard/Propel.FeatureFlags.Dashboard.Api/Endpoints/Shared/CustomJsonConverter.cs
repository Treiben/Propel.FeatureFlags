using System.Text.Json;
using System.Text.Json.Serialization;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public class CustomJsonConverter<T> : JsonConverter<T> where T : struct, Enum
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
		writer.WriteStringValue(value.ToString());
	}
}
