using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.Domain;

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

public abstract class FeatureFlagBase : IFeatureFlag
{
	public string Key { get; }
	public string? Name { get; }
	public string? Description { get; }
	public EvaluationMode OnOffMode { get; } = EvaluationMode.Off;

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
