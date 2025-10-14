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
	public string? ApplicationName { get; }
	public string? ApplicationVersion { get; }
	public Scope Scope { get; }

	public FlagIdentifier(string key, Scope scope, string? applicationName = null, string? applicationVersion = null)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Feature flag key cannot be null or empty.", nameof(key));
		}

		if (scope == Scope.Application && string.IsNullOrWhiteSpace(applicationName))
		{
			throw new ArgumentException("Application name must be provided when scope is Application.", nameof(applicationName));
		}

		Key = key.Trim();
		Scope = scope;

		ApplicationName = string.IsNullOrWhiteSpace(applicationName) ? null : applicationName!.Trim();
		ApplicationVersion = string.IsNullOrWhiteSpace(applicationVersion) ? null : applicationVersion!.Trim();
	}

	public static FlagIdentifier CreateGlobal(string key)
	{
		return new FlagIdentifier(key, Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
	}
}

public enum Scope
{
	Global,
	Feature,
	Application,
}
