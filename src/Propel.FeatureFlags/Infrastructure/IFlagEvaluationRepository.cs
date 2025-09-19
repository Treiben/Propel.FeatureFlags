using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure;

public interface IFlagEvaluationRepository
{
	Task<FlagEvaluationConfiguration?> GetAsync(FlagIdentifier flagKey, CancellationToken cancellationToken = default);

	Task CreateAsync(FlagIdentifier flagKey, EvaluationMode activationMode, string name, string description, CancellationToken cancellationToken = default);
}

public class InsertFlagException : Exception
{
	public string Key { get; }

	public Scope Scope { get; }

	public string? ApplicationName { get; }

	public string? ApplicationVersion { get; }

	public InsertFlagException(string message, Exception? innerException,
		string key, Scope scope, string? applicationName = null, string? applicationVersion = null)
		: base(message, innerException)
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}

	public InsertFlagException(
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
