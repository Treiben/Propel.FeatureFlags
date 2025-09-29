using DbMigrationCli.Models;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Migrations.CLI.Providers;
using System.Text.RegularExpressions;

namespace Propel.FeatureFlags.Migrations.CLI.Services;

public interface ISeedService
{
    Task SeedAsync(string connectionString, string provider, string seedsPath);
}

public class SeedService : ISeedService
{
    private readonly IDatabaseProviderFactory _providerFactory;
    private readonly ILogger<SeedService> _logger;

    public SeedService(IDatabaseProviderFactory providerFactory, ILogger<SeedService> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task SeedAsync(string connectionString, string provider, string seedsPath)
    {
        var dbProvider = _providerFactory.CreateProvider(provider);
        
        // Ensure database exists
        if (!await dbProvider.DatabaseExistsAsync(connectionString))
        {
            throw new InvalidOperationException("Database does not exist. Run migrations first.");
        }

        var seedFiles = GetSeedFiles(seedsPath);
        
        if (!seedFiles.Any())
        {
            _logger.LogInformation("No seed files found in {SeedsPath}", seedsPath);
            return;
        }

        foreach (var seedFile in seedFiles.OrderBy(s => s.Order).ThenBy(s => s.Name))
        {
            _logger.LogInformation("Executing seed: {Name}", seedFile.Name);
            
            try
            {
                await dbProvider.ExecuteSqlAsync(connectionString, seedFile.Script);
                _logger.LogInformation("Seed {Name} executed successfully", seedFile.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute seed {Name}: {Message}", seedFile.Name, ex.Message);
                throw;
            }
        }
    }

    private List<SeedFile> GetSeedFiles(string seedsPath)
    {
        if (!Directory.Exists(seedsPath))
        {
            _logger.LogWarning("Seeds directory does not exist: {Path}", seedsPath);
            return new List<SeedFile>();
        }

        var files = new List<SeedFile>();
        var sqlFiles = Directory.GetFiles(seedsPath, "*.sql", SearchOption.TopDirectoryOnly);

        foreach (var file in sqlFiles)
        {
            var seedFile = ParseSeedFile(file);
            if (seedFile != null)
            {
                files.Add(seedFile);
            }
        }

        return files;
    }

    private SeedFile? ParseSeedFile(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        
        // Try to extract order number from filename (e.g., 001_UserData.sql, 002_ReferenceData.sql)
        var orderMatch = Regex.Match(fileName, @"^(\d+)_(.+)$");
        
        var order = 0;
        var name = fileName;

        if (orderMatch.Success)
        {
            if (int.TryParse(orderMatch.Groups[1].Value, out var parsedOrder))
            {
                order = parsedOrder;
            }
            name = orderMatch.Groups[2].Value.Replace("_", " ");
        }

        var content = File.ReadAllText(filePath);
        
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Seed file is empty: {FileName}", fileName);
            return null;
        }

        return new SeedFile
        {
            Name = name,
            Script = content,
            FilePath = filePath,
            Order = order
        };
    }
}