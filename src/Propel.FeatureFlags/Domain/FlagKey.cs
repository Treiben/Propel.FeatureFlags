namespace Propel.FeatureFlags.Domain;

public class FlagKey
{
	public string Key { get; }
	public string? ApplicationName { get; }
	public string? ApplicationVersion { get; }
	public Scope Scope { get; }

	public FlagKey(string key, Scope scope, string? applicationName = null, string? applicationVersion = null)
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
}

public enum Scope
{
	Global,
	Feature,
	Application,
}
