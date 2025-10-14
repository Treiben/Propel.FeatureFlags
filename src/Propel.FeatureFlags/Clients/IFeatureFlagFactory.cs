using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Clients;

/// <summary>
/// Provides methods to retrieve and manage feature flags within the application.
/// </summary>
/// <remarks>This interface defines methods for accessing feature flags by key, type, or retrieving all available
/// flags. Implementations of this interface are expected to manage the lifecycle and storage of feature
/// flags.</remarks>
public interface IFeatureFlagFactory
{
	/// <summary>
	/// Retrieves the feature flag associated with the specified key.
	/// </summary>
	IFeatureFlag? GetFlagByKey(string key);

	/// <summary>
	/// Retrieves the first feature flag of the specified type from the collection.
	/// </summary>
	IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag;

	/// <summary>
	/// Retrieves all feature flags available in the system.
	/// </summary>
	IEnumerable<IFeatureFlag> GetAllFlags();

	/// <summary>
	/// Adds a feature flag to the collection of flags.
	/// </summary>
	void AddFlag(IFeatureFlag flag);
}

public class FeatureFlagFactory : IFeatureFlagFactory
{
	private readonly HashSet<IFeatureFlag> _allFlags;

	public FeatureFlagFactory(IEnumerable<IFeatureFlag> allFlags)
	{
		_allFlags = [.. allFlags];
	}

	/// <summary>
	/// Retrieves the feature flag associated with the specified key.
	/// </summary>
	/// <param name="key">The unique identifier of the feature flag to retrieve. Cannot be <see langword="null"/> or empty.</param>
	/// <returns>The feature flag that matches the specified key, or <see langword="null"/> if no matching flag is found.</returns>
	public IFeatureFlag? GetFlagByKey(string key) => _allFlags.FirstOrDefault(f => f.Key == key);

	/// <summary>
	/// Retrieves the first feature flag of the specified type from the collection.
	/// </summary>
	/// <remarks>This method searches the collection of feature flags and returns the first instance that matches
	/// the specified type. If no matching feature flag is found, the method returns <see langword="null"/>.</remarks>
	/// <typeparam name="T">The type of the feature flag to retrieve. Must implement <see cref="IFeatureFlag"/>.</typeparam>
	/// <returns>The first feature flag of type <typeparamref name="T"/> if found; otherwise, <see langword="null"/>.</returns>
	public IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag => _allFlags.OfType<T>().FirstOrDefault();

	/// <summary>
	/// Retrieves all feature flags available in the system.
	/// </summary>
	/// <returns>An <see cref="IEnumerable{T}"/> of <see cref="IFeatureFlag"/> representing all feature flags.</returns>
	public IEnumerable<IFeatureFlag> GetAllFlags() => [.. _allFlags];

	/// <summary>
	/// Adds a feature flag to the collection of flags.
	/// </summary>
	/// <param name="flag">The feature flag to add. Cannot be <see langword="null"/>.</param>
	public void AddFlag(IFeatureFlag flag) => _allFlags.Add(flag);
}
