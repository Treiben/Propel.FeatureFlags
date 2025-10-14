namespace Propel.FeatureFlags.Domain;

/// <summary>
/// Represents a feature flag used to control the availability of specific functionality  within an application.
/// Provides metadata and configuration for managing the feature  flag's lifecycle and behavior.
/// </summary>
/// <remarks>A feature flag is a mechanism for enabling or disabling application features dynamically  without
/// requiring code changes or redeployment. This interface defines the metadata  and initial configuration for a feature
/// flag, including its unique identifier, display  name, description, and default state when created.  The <see
/// cref="Key"/> property uniquely identifies the feature flag across all environments,  while the <see cref="Name"/>
/// and <see cref="Description"/> provide human-readable metadata  for management purposes. The <see cref="OnOffMode"/>
/// property specifies the default state  of the flag when it is first created.</remarks>
public interface IFeatureFlag
{
	/// <summary>
	/// Unique identifier for this feature flag across all environments
	/// </summary>
	string Key { get;}
	/// <summary>
	/// Human-readable name displayed in the management UI
	/// </summary>
	string? Name { get; }
	/// <summary>
	/// Detailed description explaining the flag's purpose and impact
	/// If left empty, the description will be auto-assigned to "No description provided"
	/// and will have to be updated later in management tools
	/// </summary>
	string? Description { get; }
	/// <summary>
	/// Determines the initial state when the flag is auto-created.
	/// EvaluationMode.Enabled = Flag starts enabled when first created
	/// EvaluationMode.Disabled = Flag starts disabled when first created (safer default)
	/// </summary>
	EvaluationMode OnOffMode { get; }
}

/// <summary>
/// Represents the base class for a feature flag, providing common properties and functionality for managing feature
/// toggles in an application.
/// </summary>
/// <remarks>This class defines the core structure for a feature flag, including its unique key, optional name and
/// description, and its default evaluation mode (on or off). Derived classes can extend this base functionality to
/// implement specific feature flag behaviors.</remarks>
public abstract class FeatureFlagBase : IFeatureFlag
{
	/// <summary>
	/// Gets the unique identifier associated with the current instance.
	/// </summary>
	public string Key { get; }

	/// <summary>
	/// Gets the name associated with the current instance. Optional - if not provided, it will default to the Key value
	/// </summary>
	public string? Name { get; }

	/// <summary>
	/// Gets the description associated with the object. Optional - if not provided, it will default to "No description provided"
	/// </summary>
	public string? Description { get; }

	/// <summary>
	/// Gets the evaluation mode indicating whether the feature is enabled or disabled on initial setup.
	/// </summary>
	public EvaluationMode OnOffMode { get; } = EvaluationMode.Off;

	/// <summary>
	/// Initializes a new instance of the <see cref="FeatureFlagBase"/> class with the specified key, name, description,
	/// and evaluation mode.
	/// </summary>
	/// <param name="key">The unique identifier for the feature flag. This value cannot be null, empty, or consist only of whitespace.</param>
	/// <param name="name">An optional display name for the feature flag. If not provided, the <paramref name="key"/> will be used as the
	/// name.</param>
	/// <param name="description">An optional description of the feature flag. If not provided or if the value is null or whitespace, a default
	/// description of "No description provided" will be used.</param>
	/// <param name="onOfMode">The default evaluation mode for the feature flag. Must be either <see cref="EvaluationMode.On"/> or <see
	/// cref="EvaluationMode.Off"/>.</param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null, empty, or consists only of whitespace. Thrown if <paramref
	/// name="onOfMode"/> is not <see cref="EvaluationMode.On"/> or <see cref="EvaluationMode.Off"/>.</exception>
	public FeatureFlagBase(
			string key,
			string? name = null,
			string? description = null,
			EvaluationMode onOfMode = EvaluationMode.Off)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Feature flag key cannot be null or empty.", nameof(key));
		}

		if (onOfMode != EvaluationMode.On && onOfMode != EvaluationMode.Off)
		{
			throw new ArgumentException("Default mode must be either Enabled or Disabled.", nameof(onOfMode));
		}

		Key = key.Trim();
		Name = name ?? Key;
		Description = string.IsNullOrWhiteSpace(description) ? "No description provided" : description!.Trim();
		OnOffMode = onOfMode;
	}
}
