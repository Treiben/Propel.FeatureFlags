using Propel.FeatureFlags.Infrastructure.Cache;

namespace Propel.FeatureFlags.Infrastructure;

/// <summary>
/// Represents the configuration settings for the Propel feature flag system.
/// </summary>
/// <remarks>This class provides options to customize the behavior of the Propel feature flag system,  including
/// flag registration, automatic deployment, type-safe flag access, local caching,  and attribute-based interception.
/// Modify these settings to tailor the system to your application's needs.</remarks>
public class PropelConfiguration
{
	/// <summary>
	/// Gets or sets a value indicating whether flags should be registered with the container.
	/// Default is true.
	/// </summary>
	public bool RegisterFlagsWithContainer { get; set; } = true;
	/// <summary>
	/// Gets or sets a value indicating whether the system should automatically deploy flags
	/// Default is false but recommended for most scenarios to set it true.
	/// </summary>
	public bool AutoDeployFlags { get; set; } = false;
	/// <summary>
	/// Register IFeatureFlagFactory for type-safe flag access. Default is true.
	/// </summary>
	public bool EnableFlagFactory { get; set; } = true;
	/// <summary>
	/// Gets or sets the configuration settings for the local cache.
	/// </summary>
	public LocalCacheConfiguration LocalCacheConfiguration { get; set; } = new LocalCacheConfiguration();
	/// <summary>
	/// Gets or sets the AOP interception options for attribute-based feature flags.
	/// </summary>
	public AOPOptions Interception { get; set; } = new AOPOptions();
}

/// <summary>
/// Provides configuration options for Aspect-Oriented Programming (AOP) features,  such as method and HTTP context
/// interception, in the application.
/// </summary>
/// <remarks>Use this optins to enable or disable specific AOP features, such as intercepting  HTTP requests or
/// general method calls. These options are typically used to  configure AOP behavior in dependency injection containers
/// or middleware.</remarks>
public class AOPOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether HTTP context interception is enabled.
	/// For example, to support attributes that extract information from HTTP requests.
	/// </summary>
	public bool EnableHttpIntercepter { get; set; } = false;
	/// <summary>
	/// Gets or sets a value indicating whether general method interception is enabled.
	/// For example, to support attributes on services or repositories.
	/// </summary>
	public bool EnableIntercepter { get; set; } = false;
}
