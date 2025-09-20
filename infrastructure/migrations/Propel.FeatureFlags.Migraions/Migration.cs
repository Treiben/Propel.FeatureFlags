namespace Propel.FeatureFlags.Migrations;

public class Migration
{
	public string Version { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
	public string SqlScript { get; set; } = string.Empty;
	public string? RollbackScript { get; set; }
	public DateTime? AppliedAt { get; set; }
	public bool IsApplied => AppliedAt.HasValue;

	public string GetSortableVersion()
	{
		// Convert V1_0_0 to 001.000.000 for proper sorting
		var parts = Version.TrimStart('V', 'v').Split('_');
		return string.Join(".", parts.Select(p => p.PadLeft(3, '0')));
	}
}

public class MigrationResult
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public List<string> Errors { get; set; } = [];
	public TimeSpan Duration { get; set; }
	public int MigrationsApplied { get; set; }

	public static MigrationResult Ok() => new() { Success = true };

	public static MigrationResult Failed(string because) => new() { Success = false, Errors = [because]};
}
