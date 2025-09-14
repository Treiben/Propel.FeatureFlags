using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Services.ApplicationScope;

public interface IApplicationFeatureFlag
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
	/// Metadata tags for categorization and filtering in management tools
	/// </summary>
	Dictionary<string, string>? Tags { get; }
	/// <summary>
	/// Determines the initial state when the flag is auto-created.
	/// EvaluationMode.Enabled = Flag starts enabled when first created
	/// EvaluationMode.Disabled = Flag starts disabled when first created (safer default)
	/// </summary>
	EvaluationMode DefaultMode { get; }
}

public abstract class TypeSafeFeatureFlag : IApplicationFeatureFlag
{
	public string Key { get; }
	public string? Name { get; }
	public string? Description { get; }
	public Dictionary<string, string>? Tags { get; }
	public EvaluationMode DefaultMode { get; }

	public TypeSafeFeatureFlag(
			string key,
			string? name = null,
			string? description = null,
			Dictionary<string, string>? tags = null,
			EvaluationMode defaultMode = EvaluationMode.Disabled)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Feature flag key cannot be null or empty.", nameof(key));
		}

		if (defaultMode != EvaluationMode.Enabled && defaultMode != EvaluationMode.Disabled)
		{
			throw new ArgumentException("Default mode must be either Enabled or Disabled.", nameof(defaultMode));
		}

		Key = key.Trim();
		Name = name ?? Key;
		Description = string.IsNullOrWhiteSpace(description) ? "No description provided" : description!.Trim();
		Tags = tags;
		DefaultMode = defaultMode;
	}
}
