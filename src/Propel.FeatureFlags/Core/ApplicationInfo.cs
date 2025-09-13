using System.Diagnostics;
using System.Reflection;

namespace Propel.FeatureFlags.Core;

public static class ApplicationInfo
{
	private static readonly Lazy<(string name, string version)> _applicationInfo = new(GetApplicationInfo);

	public static string Name => _applicationInfo.Value.name;
	public static string Version => _applicationInfo.Value.version;

	private static (string name, string version) GetApplicationInfo()
	{
		// Try to get the entry assembly first (main application)
		var entryAssembly = Assembly.GetEntryAssembly();
		if (entryAssembly != null)
		{
			var name = entryAssembly.GetName().Name ?? "Unknown";
			var version = entryAssembly.GetName().Version?.ToString() ?? "1.0.0";
			return (name, version);
		}

		// Fallback to calling assembly
		var callingAssembly = Assembly.GetCallingAssembly();
		if (callingAssembly != null)
		{
			var name = callingAssembly.GetName().Name ?? "Unknown";
			var version = callingAssembly.GetName().Version?.ToString() ?? "1.0.0";
			return (name, version);
		}

		// Final fallback to process name
		var processName = Process.GetCurrentProcess().ProcessName;
		return (processName, "1.0.0");
	}
}