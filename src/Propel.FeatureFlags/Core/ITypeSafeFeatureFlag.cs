namespace Propel.FeatureFlags.Core;

public interface ITypeSafeFeatureFlag
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
	/// Application identifier for multi-app feature flag management
	/// If left empty, the application name will be auto-assigned based on the calling assembly
	/// </summary>
	string? ApplicationName { get; }
	/// <summary>
	/// Application version for tracking flag lifecycle across deployments
	/// Can be empty if version not applicable
	/// </summary>
	string? ApplicationVersion { get; }
	/// <summary>
	/// Metadata tags for categorization and filtering in management tools
	/// </summary>
	Dictionary<string, string>? Tags { get; }
	Audit Created { get; }
	/// <summary>
	/// Determines the initial state when the flag is auto-created.
	/// true = Flag starts enabled when first created
	/// false = Flag starts disabled when first created (safer default)
	/// </summary>
	bool IsEnabledOnCreation { get; }
}

public abstract class TypeSafeFeatureFlag : ITypeSafeFeatureFlag
{
	public string Key { get; }
	public string? Name { get; }
	public string? Description { get; }
	public string? ApplicationName { get; }
	public string? ApplicationVersion { get; }
	public Dictionary<string, string>? Tags { get; }
	public Audit Created { get; }
	public bool IsEnabledOnCreation { get; }

	public TypeSafeFeatureFlag(
			string key,
			string? name = null,
			string? description = null,
			string? applicationName = null,
			string? applicationVersion = null,
			Dictionary<string, string>? tags = null,
			string? createdBy = null,
			bool isEnabledOnCreation = false)
	{
		// Validate and normalize the key
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Feature flag key cannot be null or empty.", nameof(key));
		}
		Key = key.Trim();

		// Apply defaults and validate other properties
		Name = name ?? Key;
		Description = string.IsNullOrWhiteSpace(description) ? "No description provided" : description!.Trim();
		ApplicationName = applicationName ?? ApplicationInfo.Name;
		ApplicationVersion = applicationVersion ?? ApplicationInfo.Version;
		Tags = tags;
		Created = Audit.FlagCreated(createdBy);
		IsEnabledOnCreation = isEnabledOnCreation;
	}
}
