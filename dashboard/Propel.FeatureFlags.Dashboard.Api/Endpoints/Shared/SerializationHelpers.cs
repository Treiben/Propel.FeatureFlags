namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public static class SerializationHelpers
{
	public static bool TryDeserialize<T>(string json, out T? result)
	{
		try
		{
			result = System.Text.Json.JsonSerializer.Deserialize<T>(json);
			return result != null;
		}
		catch (System.Text.Json.JsonException)
		{
			result = default;
			return false;
		}
	}
}
