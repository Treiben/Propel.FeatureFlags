using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public class DuplicatedFeatureFlagException : Exception
{
	public string Key { get; }

	public Scope Scope { get; }

	public string? ApplicationName { get; }

	public string? ApplicationVersion { get; }

	public DuplicatedFeatureFlagException(string key,
		Scope scope,
		string? applicationName = null,
		string? applicationVersion = null) : base("Cannot create a duplicated feature flag.")
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}
}

public class FlagInsertException : Exception
{
	public string Key { get; }

	public Scope Scope { get; }

	public string? ApplicationName { get; }

	public string? ApplicationVersion { get; }

	public FlagInsertException(string message, Exception? innerException,
		string key, Scope scope, string? applicationName = null, string? applicationVersion = null)
		: base(message, innerException)
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}

	public FlagInsertException(
		string message,
		string key,
		Scope scope,
		string? applicationName = null,
		string? applicationVersion = null)
	: base(message)
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}
}

public class FlagUpdateException : Exception
{
	public string Key { get; }
	public Scope Scope { get; }
	public string? ApplicationName { get; }
	public string? ApplicationVersion { get; }
	public FlagUpdateException(string message, Exception? innerException,
		string key, Scope scope, string? applicationName = null, string? applicationVersion = null)
		: base(message, innerException)
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}
	public FlagUpdateException(
		string message,
		string key,
		Scope scope,
		string? applicationName = null,
		string? applicationVersion = null)
	: base(message)
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}
}
