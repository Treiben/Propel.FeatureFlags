using System.Security.Cryptography;
using System.Text;

namespace Propel.FeatureFlags.Utilities;

public static class Hasher
{
	public static uint ComputeHash(string input)
	{
		using var sha256 = SHA256.Create();
		var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
		return BitConverter.ToUInt32(hash, 0);
	}
}
