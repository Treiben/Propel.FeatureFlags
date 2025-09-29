namespace DbMigrationCli.Models;

public class AppliedMigration
{
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
}

public class MigrationFile
{
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UpScript { get; set; } = string.Empty;
    public string DownScript { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public class MigrationStatus
{
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
}

public class SeedFile
{
    public string Name { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Order { get; set; }
}