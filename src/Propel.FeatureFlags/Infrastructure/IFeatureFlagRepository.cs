using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure;

/// <summary>
/// Defines methods for managing and retrieving feature flags and their evaluation options.
/// </summary>
/// <remarks>This interface provides functionality to retrieve evaluation options for a specific feature flag and
/// to create new application-level feature flags with associated metadata.</remarks>
public interface IFeatureFlagRepository
{
	/// <summary>
	/// Asynchronously retrieves the evaluation options associated with the specified key.
	/// </summary>
	/// <remarks>This method performs an asynchronous operation and should be awaited. If the specified key does not
	/// exist, the method returns <see langword="null"/>.</remarks>
	/// <param name="key">The unique identifier for the evaluation options to retrieve. Cannot be <see langword="null"/> or empty.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
	/// <returns>An <see cref="EvaluationOptions"/> object containing the evaluation options if found; otherwise, <see
	/// langword="null"/>.</returns>
	Task<EvaluationOptions?> GetEvaluationOptionsAsync(string key, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a new application flag with the specified key, evaluation mode, name, and description.
	/// </summary>
	/// <remarks>The <paramref name="key"/> parameter must be unique within the application. If a flag with the same
	/// key already exists, the behavior of this method is undefined. The <paramref name="activeMode"/> parameter
	/// determines how the flag is evaluated, which may affect its behavior in runtime scenarios.</remarks>
	/// <param name="key">The unique identifier for the application flag. Must not be null or empty.</param>
	/// <param name="activeMode">The evaluation mode that determines how the flag will be processed.</param>
	/// <param name="name">The display name of the application flag. Must not be null or empty.</param>
	/// <param name="description">A description of the application flag. Can be null or empty.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	Task CreateApplicationFlagAsync(string key, EvaluationMode activeMode, string name, string description, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an exception that occurs when an application flag is invalid or cannot be resolved.
/// </summary>
/// <remarks>This exception is typically thrown when an application flag, identified by a specific key and scope, 
/// is found to be in an invalid state or when its resolution fails. It provides additional context  about the flag,
/// including its key, scope, and optionally the application name and version.</remarks>
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
