namespace Propel.FeatureFlags.Domain;

/// A feature represents a distinct functionality or capability within the application that can be toggled on or off.
/// NOT IMPLEMENTED YET
public class Feature
{
	public string Key { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public bool IsReleased { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
