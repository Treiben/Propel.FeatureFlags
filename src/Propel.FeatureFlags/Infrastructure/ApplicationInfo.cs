using System.Diagnostics;
using System.Reflection;

namespace Propel.FeatureFlags.Infrastructure;

/// <summary>
/// Provides information about the current application, including its name and version.
/// </summary>
/// <remarks>The application name and version are determined using the following precedence: <list type="number">
/// <item> <description>Environment variables <c>APP_NAME</c> and <c>APP_VERSION</c>, if both are set.</description>
/// </item> <item> <description>The <c>APP_NAME</c> environment variable with a default version of <c>1.0.0.0</c>, if
/// only <c>APP_NAME</c> is set.</description> </item> <item> <description>The entry assembly's name and version, if
/// available.</description> </item> <item> <description>The calling assembly's name and version, if the entry assembly
/// is not available.</description> </item> <item> <description>The current process name with a default version of
/// <c>1.0.0.0</c>, as a final fallback.</description> </item> </list> This class is thread-safe and uses lazy
/// initialization to retrieve the application information only once.</remarks>
public static class ApplicationInfo
{
	private static readonly Lazy<(string name, string version)> _applicationInfo = new(GetApplicationInfo);

	public static string Name => _applicationInfo.Value.name;
	public static string Version => _applicationInfo.Value.version;

	/// <summary>
	/// Retrieves the application's name and version information.
	/// </summary>
	/// <remarks>This method attempts to determine the application's name and version using the following sources,
	/// in order: <list type="bullet"> <item><description>Environment variables <c>APP_NAME</c> and <c>APP_VERSION</c>, if
	/// both are set.</description></item> <item><description>The <c>APP_NAME</c> environment variable with a default
	/// version of <c>1.0.0.0</c>, if only <c>APP_NAME</c> is set.</description></item> <item><description>The entry
	/// assembly's name and version, if available.</description></item> <item><description>The calling assembly's name and
	/// version, if the entry assembly is unavailable.</description></item> <item><description>The current process name
	/// with a default version of <c>1.0.0.0</c>, as a final fallback.</description></item> </list></remarks>
	/// <returns>A tuple containing the application's name and version. The name is a string representing the application name, and
	/// the version is a string representing the version number.</returns>
	private static (string name, string version) GetApplicationInfo()
	{
		var envName = Environment.GetEnvironmentVariable("APP_NAME");
		var envVersion = Environment.GetEnvironmentVariable("APP_VERSION");

		if (envName != null && envVersion != null)
		{
			return (envName, envVersion);
		}

		if (envName != null)
		{
			return (envName, "1.0.0.0");
		}

		// Try to get the entry assembly first (main application)
		var entryAssembly = Assembly.GetEntryAssembly();
		if (entryAssembly != null)
		{
			var name = entryAssembly.GetName().Name ?? "Unknown";
			var version = entryAssembly.GetName().Version?.ToString() ?? "1.0.0.0";
			return (name, version);
		}

		// Fallback to calling assembly
		var callingAssembly = Assembly.GetCallingAssembly();
		if (callingAssembly != null)
		{
			var name = callingAssembly.GetName().Name ?? "Unknown";
			var version = callingAssembly.GetName().Version?.ToString() ?? "1.0.0.0";
			return (name, version);
		}

		// Final fallback to process name
		var processName = Process.GetCurrentProcess().ProcessName;
		return (processName, "1.0.0.0");
	}
}