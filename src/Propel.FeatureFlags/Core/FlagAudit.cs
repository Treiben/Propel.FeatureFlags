namespace Propel.FeatureFlags.Core;

public class FlagAudit
{
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public string CreatedBy { get; set; } = string.Empty;
	public string UpdatedBy { get; set; } = string.Empty;

	public static FlagAudit New => new FlagAudit
	{
		CreatedAt = DateTime.UtcNow,
		UpdatedAt = DateTime.MinValue,
		CreatedBy = "system",
		UpdatedBy = ""
	};
}
