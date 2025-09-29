using DbMigrationCli.Models;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations.CLI.Providers;
using System.Text.RegularExpressions;

namespace Propel.FeatureFlags.Migrations.CLI.Services;

public interface IMigrationService
{
    Task MigrateAsync(string connectionString, string provider, string migrationsPath, string? targetVersion = null);
    Task RollbackAsync(string connectionString, string provider, string migrationsPath, string targetVersion);
    Task BaselineAsync(string connectionString, string provider, string version);
    Task<IEnumerable<MigrationStatus>> GetStatusAsync(string connectionString, string provider, string migrationsPath);
}

public class MigrationService : IMigrationService
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(IDatabaseProviderFactory providerFactory, ILogger<MigrationService> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task MigrateAsync(string connectionString,
        string provider, string migrationsPath, string? targetVersion = null)
    {
        var dbProvider = _providerFactory.CreateProvider(provider);

        // Ensure database exists
        if (!await dbProvider.DatabaseExistsAsync(connectionString))
        {
            await dbProvider.CreateDatabaseAsync(connectionString);
        }

        // Ensure migration table exists
        if (!await dbProvider.MigrationTableExistsAsync(connectionString))
        {
            await dbProvider.CreateMigrationTableAsync(connectionString);
        }

        var migrationFiles = GetMigrationFiles(migrationsPath);
        var appliedMigrations = await dbProvider.GetAppliedMigrationsAsync(connectionString);
        var appliedVersions = appliedMigrations.Select(m => m.Version).ToHashSet();

        var migrationsToRun = migrationFiles
            .Where(m => !appliedVersions.Contains(m.Version));

        if (!string.IsNullOrEmpty(targetVersion))
        {
            migrationsToRun = migrationsToRun.Where(m => string.Compare(m.Version, targetVersion, StringComparison.Ordinal) <= 0);
        }

        migrationsToRun = migrationsToRun.OrderBy(m => m.Version);

		foreach (var migration in migrationsToRun)
        {
            _logger.LogInformation("Applying migration {Version}: {Description}", migration.Version, migration.Description);
            
            await dbProvider.ExecuteSqlAsync(connectionString, migration.UpScript);
            await dbProvider.AddMigrationRecordAsync(connectionString, migration.Version, migration.Description);
            
            _logger.LogInformation("Migration {Version} applied successfully", migration.Version);
        }
    }

    public async Task RollbackAsync(string connectionString, string provider, string migrationsPath,
        string targetVersion)
    {
        var dbProvider = _providerFactory.CreateProvider(provider);
        
        if (!await dbProvider.MigrationTableExistsAsync(connectionString))
        {
            throw new InvalidOperationException("Migration table does not exist. Cannot rollback.");
        }

        var migrationFiles = GetMigrationFiles(migrationsPath);
        var appliedMigrations = await dbProvider.GetAppliedMigrationsAsync(connectionString);
        
        var migrationsToRollback = appliedMigrations
            .Where(m => string.Compare(m.Version, targetVersion, StringComparison.Ordinal) > 0)
            .OrderByDescending(m => m.Version);

        foreach (var appliedMigration in migrationsToRollback)
        {
            var migrationFile = migrationFiles.FirstOrDefault(m => m.Version == appliedMigration.Version);
            if (migrationFile == null)
            {
                _logger.LogWarning("Migration file not found for version {Version}, skipping rollback", appliedMigration.Version);
                continue;
            }

            if (string.IsNullOrEmpty(migrationFile.DownScript))
            {
                throw new InvalidOperationException($"No down script found for migration {appliedMigration.Version}");
            }

            _logger.LogInformation("Rolling back migration {Version}: {Description}", appliedMigration.Version, appliedMigration.Description);
            
            await dbProvider.ExecuteSqlAsync(connectionString, migrationFile.DownScript);
            await dbProvider.RemoveMigrationRecordAsync(connectionString, appliedMigration.Version);
            
            _logger.LogInformation("Migration {Version} rolled back successfully", appliedMigration.Version);
        }
    }

    public async Task BaselineAsync(string connectionString, string provider, string version)
    {
        var dbProvider = _providerFactory.CreateProvider(provider);
        
        // Ensure database exists
        if (!await dbProvider.DatabaseExistsAsync(connectionString))
        {
            await dbProvider.CreateDatabaseAsync(connectionString);
        }

        // Ensure migration table exists
        if (!await dbProvider.MigrationTableExistsAsync(connectionString))
        {
            await dbProvider.CreateMigrationTableAsync(connectionString);
        }

        await dbProvider.AddMigrationRecordAsync(connectionString, version, "Baseline");
        _logger.LogInformation("Baseline migration {Version} created", version);
    }

    public async Task<IEnumerable<MigrationStatus>> GetStatusAsync(string connectionString, string provider, string migrationsPath)
    {
        var dbProvider = _providerFactory.CreateProvider(provider);
        
        var migrationFiles = GetMigrationFiles(migrationsPath);
        var appliedMigrations = new List<AppliedMigration>();

        if (await dbProvider.DatabaseExistsAsync(connectionString) && 
            await dbProvider.MigrationTableExistsAsync(connectionString))
        {
            appliedMigrations = (await dbProvider.GetAppliedMigrationsAsync(connectionString)).ToList();
        }

        var appliedVersions = appliedMigrations.ToDictionary(m => m.Version, m => m);

        var status = new List<MigrationStatus>();

        // Add all migration files
        foreach (var file in migrationFiles)
        {
            var isApplied = appliedVersions.ContainsKey(file.Version);
            status.Add(new MigrationStatus
            {
                Version = file.Version,
                Description = file.Description,
                IsApplied = isApplied,
                AppliedAt = isApplied ? appliedVersions[file.Version].AppliedAt : null
            });
        }

        // Add any applied migrations that don't have files (orphaned)
        foreach (var applied in appliedMigrations)
        {
            if (!migrationFiles.Any(f => f.Version == applied.Version))
            {
                status.Add(new MigrationStatus
                {
                    Version = applied.Version,
                    Description = $"{applied.Description} (ORPHANED - file not found)",
                    IsApplied = true,
                    AppliedAt = applied.AppliedAt
                });
            }
        }

        return status;
    }

    private List<MigrationFile> GetMigrationFiles(string migrationsPath)
    {
        if (!Directory.Exists(migrationsPath))
        {
            _logger.LogWarning("Migrations directory does not exist: {Path}", migrationsPath);
            return new List<MigrationFile>();
        }

        var files = new List<MigrationFile>();
        var sqlFiles = Directory.GetFiles(migrationsPath, "*.sql", SearchOption.TopDirectoryOnly);

        foreach (var file in sqlFiles)
        {
            var migration = ParseMigrationFile(file);
            if (migration != null)
            {
                files.Add(migration);
            }
        }

        return files.OrderBy(f => f.Version).ToList();
    }

    private MigrationFile? ParseMigrationFile(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        
        // Expected format: YYYYMMDDHHMMSS_Description.sql
        var match = Regex.Match(fileName, @"^(\d{14})_(.+)$");
        if (!match.Success)
        {
            _logger.LogWarning("Invalid migration file name format: {FileName}", fileName);
            return null;
        }

        var version = match.Groups[1].Value;
        var description = match.Groups[2].Value.Replace("_", " ");

        var content = File.ReadAllText(filePath);
        var (upScript, downScript) = SplitMigrationContent(content);

        return new MigrationFile
        {
            Version = version,
            Description = description,
            UpScript = upScript,
            DownScript = downScript,
            FilePath = filePath
        };
    }

    private (string upScript, string downScript) SplitMigrationContent(string content)
    {
        // Look for -- DOWN marker to split up and down scripts
        var downMarkerIndex = content.IndexOf("-- DOWN", StringComparison.OrdinalIgnoreCase);
        
        if (downMarkerIndex == -1)
        {
            // No down script, entire content is up script
            return (content.Trim(), string.Empty);
        }

        var upScript = content.Substring(0, downMarkerIndex).Trim();
        var downScript = content.Substring(downMarkerIndex + 7).Trim(); // 7 = length of "-- DOWN"

        return (upScript, downScript);
    }
}