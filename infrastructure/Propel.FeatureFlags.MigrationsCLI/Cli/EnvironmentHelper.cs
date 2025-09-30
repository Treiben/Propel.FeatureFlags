namespace Propel.FeatureFlags.MigrationsCLI.Cli;

public static class EnvironmentHelper
{
    /// <summary>
    /// Gets connection string from environment variable if not provided directly
    /// Environment variable names: DB_CONNECTION_STRING, DATABASE_URL
    /// </summary>
    public static string GetConnectionString(string? connectionString)
    {
        if (!string.IsNullOrEmpty(connectionString))
        {
            return connectionString;
        }

        // Try common environment variable names
        var envConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") ??
                                 Environment.GetEnvironmentVariable("DATABASE_URL") ??
                                 Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrEmpty(envConnectionString))
        {
            throw new ArgumentException("Connection string must be provided either as parameter or environment variable (DB_CONNECTION_STRING, DATABASE_URL, or ConnectionStrings__Default)");
        }

        return envConnectionString;
    }

    /// <summary>
    /// Gets database provider from environment variable if not provided directly
    /// Environment variable name: DB_PROVIDER
    /// </summary>
    public static string GetProvider(string? provider)
    {
        if (!string.IsNullOrEmpty(provider))
        {
            return provider;
        }

        var envProvider = Environment.GetEnvironmentVariable("DB_PROVIDER");

        if (string.IsNullOrEmpty(envProvider))
        {
            throw new ArgumentException("Database provider must be provided either as parameter or environment variable (DB_PROVIDER)");
        }

        return envProvider;
    }

    /// <summary>
    /// Gets migrations path from environment variable if not provided directly
    /// Environment variable name: MIGRATIONS_PATH
    /// </summary>
    public static string GetMigrationsPath(string? migrationsPath)
    {
        if (!string.IsNullOrEmpty(migrationsPath))
        {
            return migrationsPath;
        }

        return Environment.GetEnvironmentVariable("MIGRATIONS_PATH") ?? "./Migrations";
    }

    /// <summary>
    /// Gets seeds path from environment variable if not provided directly
    /// Environment variable name: SEEDS_PATH
    /// </summary>
    public static string GetSeedsPath(string? seedsPath)
    {
        if (!string.IsNullOrEmpty(seedsPath))
        {
            return seedsPath;
        }

        return Environment.GetEnvironmentVariable("SEEDS_PATH") ?? "./Seeds";
    }

    /// <summary>
    /// Checks if running in CI/CD environment
    /// </summary>
    public static bool IsRunningInCiCd()
    {
        var ciIndicators = new[]
        {
            "CI", "CONTINUOUS_INTEGRATION", "BUILD_ID", "BUILD_NUMBER",
            "GITHUB_ACTIONS", "GITLAB_CI", "AZURE_PIPELINE", "JENKINS_URL",
            "TEAMCITY_VERSION", "TRAVIS", "CIRCLECI"
        };

        return ciIndicators.Any(indicator => 
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(indicator)));
    }

    /// <summary>
    /// Gets all environment variables that start with DB_ for debugging
    /// </summary>
    public static Dictionary<string, string> GetDatabaseEnvironmentVariables()
    {
        return Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key.ToString()?.StartsWith("DB_", StringComparison.OrdinalIgnoreCase) == true)
            .ToDictionary(
                entry => entry.Key.ToString()!,
                entry => entry.Value?.ToString() ?? string.Empty
            );
    }
}