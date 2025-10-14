namespace Propel.FeatureFlags.AspNetCore.Middleware;

public static class MiddleWareUtils
{
	/// <summary>
	/// Determines the country associated with the specified IP address.
	/// </summary>
	/// <remarks>This method uses a simplified mapping for demonstration purposes and defaults to "US" for
	/// unrecognized or unspecified IP addresses. In a production environment, consider integrating a reliable GeoIP
	/// service for accurate results.</remarks>
	/// <param name="ipAddress">The IP address to analyze. Can be <see langword="null"/>.</param>
	/// <returns>A string representing the country code (e.g., "US") associated with the IP address. Returns <see langword="null"/>
	/// if <paramref name="ipAddress"/> is <see langword="null"/>.</returns>
	public static string? GetCountryFromIP(System.Net.IPAddress? ipAddress)
	{
		// In production, use a real GeoIP service like MaxMind
		return ipAddress?.ToString() switch
		{
			var ip when ip.StartsWith("192.168") => "US", // Local dev
			_ => "US" // Default
		};
	}
}
