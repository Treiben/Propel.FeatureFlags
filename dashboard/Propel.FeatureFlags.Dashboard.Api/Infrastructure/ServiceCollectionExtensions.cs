using Propel.FeatureFlags.Dashboard.Api.Infrastructure.Postgres;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure.SqlServer;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddDatabase(this IServiceCollection services, PropelOptions options)
	{
		var connectionString = options.Database.ConnectionString
			?? throw new InvalidOperationException("Database connection string is required");

		var databaseProvider = options.Database.Provider ?? DetectDatabaseProvider(connectionString);

		return databaseProvider switch
		{
			DatabaseProvider.PostgreSQL => services.AddPostgresDbContext(connectionString),
			DatabaseProvider.SqlServer => services.AddSqlServerDbContext(connectionString),
			_ => throw new NotSupportedException($"Database provider '{databaseProvider}' is not supported")
		};
	}

	private static DatabaseProvider DetectDatabaseProvider(string connectionString)
	{
		if (string.IsNullOrWhiteSpace(connectionString))
			throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

		var lowerConnectionString = connectionString.ToLowerInvariant();

		// PostgreSQL indicators
		if (lowerConnectionString.Contains("host=") ||
			lowerConnectionString.Contains("server=") && lowerConnectionString.Contains("port=") ||
			lowerConnectionString.Contains("database=") && !lowerConnectionString.Contains("data source="))
		{
			return DatabaseProvider.PostgreSQL;
		}

		// SQL Server indicators
		if (lowerConnectionString.Contains("data source=") ||
			lowerConnectionString.Contains("server=") && !lowerConnectionString.Contains("port=") ||
			lowerConnectionString.Contains("initial catalog=") ||
			lowerConnectionString.Contains("database=") && lowerConnectionString.Contains("integrated security="))
		{
			return DatabaseProvider.SqlServer;
		}

		// Fallback: try to parse as PostgreSQL first, then SQL Server
		throw new NotSupportedException($"Could not detect database provider from connection string. Supported providers: PostgreSQL, SQL Server");
	}
}
