using Microsoft.Extensions.Logging;

namespace Propel.FeatureFlags.Migrations.CLI.Providers;

public interface IDatabaseProviderFactory
{
    IDatabaseProvider CreateProvider(string provider);
}

public class DatabaseProviderFactory : IDatabaseProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public DatabaseProviderFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IDatabaseProvider CreateProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "sqlserver" => new SqlServerProvider(_loggerFactory.CreateLogger<SqlServerProvider>()),
            "postgresql" => new PostgreSqlProvider(_loggerFactory.CreateLogger<PostgreSqlProvider>()),
            _ => throw new ArgumentException($"Unsupported database provider: {provider}")
        };
    }
}