using System.Security.Cryptography;
using System.Text;

namespace Propel.FeatureFlags.Utilities;

/// <summary>
/// Provides functionality to compute a hash value for a given input string using the SHA-256 algorithm.
/// </summary>
/// <remarks>This class is designed for generating a 32-bit hash value derived from the SHA-256 hash of the input
/// string. The hash value is suitable for scenarios where a non-cryptographic, fixed-size hash is required.</remarks>
public static class Hasher
{
	public static uint ComputeHash(string input)
	{
		using var sha256 = SHA256.Create();
		var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
		return BitConverter.ToUInt32(hash, 0);
	}
}
