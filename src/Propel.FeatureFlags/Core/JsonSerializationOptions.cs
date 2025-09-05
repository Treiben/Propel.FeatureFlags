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
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
		}
	};
}
