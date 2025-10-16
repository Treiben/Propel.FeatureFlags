using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.Domain;

/// <summary>
/// Represents a unique identifier for a feature flag, including its key, scope, and optional application context.
/// </summary>
/// <remarks>A <see cref="FlagIdentifier"/> is used to uniquely identify a feature flag within a specific scope. 
/// The scope determines whether the flag is global or tied to a specific application.  When the scope is <see
/// cref="Scope.Application"/>, the application name must be provided.</remarks>
public class FlagIdentifier
{
	public string Key { get; }
	public string ApplicationName { get; }
	public string ApplicationVersion { get; }
	public Scope Scope { get; }

	protected FlagIdentifier(string key, Scope scope, string applicationName, string applicationVersion)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Feature flag key cannot be null or empty.", nameof(key));
		}

		Key = key.Trim();
		Scope = scope;

		ApplicationName = applicationName.Trim();
		ApplicationVersion = applicationVersion!.Trim();
	}
}

public class GlobalFlagIdentifier(string key) 
	: FlagIdentifier(key, Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0")
{
}

public class ApplicationFlagIdentifier(string key, string? applicationName = null, string? applicationVersion = null) 
	: FlagIdentifier(key, Scope.Application, applicationName ?? ApplicationInfo.Name, applicationVersion ?? ApplicationInfo.Version ?? "1.0.0.0")
{
}

public enum Scope
{
	Global,
	Feature,
	Application,
}
