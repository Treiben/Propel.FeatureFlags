using System.Text;

namespace Propel.FeatureFlags.Infrastructure.Cache;

public class CacheKey
{
	private const string KEY_PREFIX = "ff";

	public const string Pattern = $"{KEY_PREFIX}:*";

	public string Key { get; }

	public string[]? Components {get; }


	public CacheKey(string key, string[]? components = null)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Cache key cannot be null or empty.", nameof(key));
		}
		Key = key.Trim();
		Components = components ?? [];
	}

	public string ComposeKey()
	{
		if (Components == null || Components.Length == 0)
		{
			return $"{KEY_PREFIX}:{Key}";
		}
		var sb = new StringBuilder(KEY_PREFIX);
		foreach (var component in Components)
		{
			if (!string.IsNullOrWhiteSpace(component))
			{
				sb.Append(":");
				sb.Append(component.Trim());
			}
		}
		if (sb.Length > 0)
		{
			sb.Append(":");
		}
		sb.Append(Key);
		return sb.ToString();
	} 
}
