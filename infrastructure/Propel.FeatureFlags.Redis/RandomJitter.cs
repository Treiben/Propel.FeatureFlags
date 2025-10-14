namespace Propel.FeatureFlags.Redis;

/// <summary>
/// Provides functionality to generate random integers within a specified range, with thread-safe access.
/// </summary>
/// <remarks>This class ensures thread safety when generating random numbers by synchronizing access to the
/// underlying random number generator. It is useful in scenarios where multiple threads require random
/// values.</remarks>
internal static class RandomJitter
{
	private static readonly Random _random = new();
	public static int Next(int minValue, int maxValue)
	{
		lock (_random)
		{
			return _random.Next(minValue, maxValue);
		}
	}
}