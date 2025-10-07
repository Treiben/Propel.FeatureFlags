using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure;

public interface IFeatureFlagRepository
{
	Task<EvaluationOptions?> GetEvaluationOptionsAsync(string key, CancellationToken cancellationToken = default);

	Task CreateApplicationFlagAsync(string key, EvaluationMode activeMode, string name, string description, CancellationToken cancellationToken = default);
}

public class ApplicationFlagException : Exception
{
	public string Key { get; }

	public Scope Scope { get; }

	public string? ApplicationName { get; }

	public string? ApplicationVersion { get; }

	public ApplicationFlagException(string message, Exception? innerException,
		string key, Scope scope, string? applicationName = null, string? applicationVersion = null)
		: base(message, innerException)
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}

	public ApplicationFlagException(
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
