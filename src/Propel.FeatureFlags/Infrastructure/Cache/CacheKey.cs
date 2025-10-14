using System.Text;

namespace Propel.FeatureFlags.Infrastructure.Cache;

/// <summary>
/// Represents a cache key with an optional set of components, used to generate a unique key for caching purposes.
/// </summary>
/// <remarks>The <see cref="CacheKey"/> class provides a way to construct cache keys with a consistent prefix and
/// optional components. This ensures that cache keys are namespaced and can include additional context when
/// necessary.</remarks>
public class CacheKey
{
	public const string KEY_PREFIX = "propel-flags";

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

	/// <summary>
	/// Composes a unique key string by combining a predefined prefix, optional components, and a base key.
	/// </summary>
	/// <remarks>The resulting key string is constructed by appending the <c>KEY_PREFIX</c>, followed by each
	/// non-empty component in <see cref="Components"/>, and finally the <see cref="Key"/>. Components are trimmed of
	/// whitespace, and periods (<c>.</c>) in the resulting key are replaced with hyphens (<c>-</c>).</remarks>
	/// <returns>A string representing the composed key. If <see cref="Components"/> is null or empty, the result will be in the
	/// format "<c>KEY_PREFIX:Key</c>". Otherwise, the result will include the components in the format
	/// "<c>KEY_PREFIX:Component1:Component2:...:Key</c>".</returns>
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
		sb.Replace(".", "-");

		return sb.ToString();
	} 
}

/// <summary>
/// Represents a cache key that is globally scoped, ensuring the key is unique across all contexts.
/// </summary>
/// <remarks>This class is used to define a cache key that applies globally, rather than being scoped to a
/// specific context or namespace.  It inherits from <see cref="CacheKey"/> and automatically includes the "global"
/// scope.</remarks>
/// <param name="key"></param>
public class GlobalCacheKey(string key) : CacheKey(key, ["global"])
{
}

/// <summary>
/// Represents a cache key that is scoped to a specific application and optionally its version.
/// </summary>
/// <remarks>This class is used to create a cache key that includes the application name and, optionally, the
/// application version. The key ensures that cached data is uniquely associated with a specific application
/// context.</remarks>
public class ApplicationCacheKey : CacheKey
{
	public ApplicationCacheKey(string key, string applicationName, string? applicationVersion = null)
		: base(key, applicationVersion == null ? ["app", applicationName] : ["app", applicationName, applicationVersion])
	{
		if (string.IsNullOrWhiteSpace(applicationName))
		{
			throw new ArgumentException("Application name cannot be null or empty.", nameof(applicationName));
		}
	}
}
