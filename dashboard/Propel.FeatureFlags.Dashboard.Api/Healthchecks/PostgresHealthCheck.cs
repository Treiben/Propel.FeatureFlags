using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using System.Diagnostics;

namespace Propel.FeatureFlags.Dashboard.Api.Healthchecks;

/// <summary>
/// Deep health check for PostgreSQL that verifies not just connection but also query execution and schema status
/// </summary>
public class PostgresDeepHealthCheck(IConfiguration configuration, ILogger<PostgresDeepHealthCheck> logger) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		var connectionString = configuration.GetConnectionString("PostgresDb");
		if (string.IsNullOrEmpty(connectionString))
		{
			return HealthCheckResult.Unhealthy("PostgreSQL connection string is missing");
		}

		var data = new Dictionary<string, object>();

		try
		{
			using var connection = new NpgsqlConnection(connectionString);
			await connection.OpenAsync(cancellationToken);

			// Check basic connectivity
			var serverVersion = connection.PostgreSqlVersion.ToString();
			data.Add("serverVersion", serverVersion);

			// Check for active connections
			using (var cmd = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity", connection))
			{
				var activeConnections = await cmd.ExecuteScalarAsync(cancellationToken);
				data.Add("activeConnections", activeConnections ?? 0);
			}

			// Check transaction capability
			using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
			{
				using var cmd = new NpgsqlCommand("SELECT 1", connection, transaction as NpgsqlTransaction);
				await cmd.ExecuteScalarAsync(cancellationToken);
				await transaction.CommitAsync(cancellationToken);
			}
			data.Add("transactionsWorking", true);

			// Check specific tables existence - key tables for your application
			using (var cmd = new NpgsqlCommand(
				"SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Customers')",
				connection))
			{
				var customersTableExists = await cmd.ExecuteScalarAsync(cancellationToken);
				data.Add("customersTableExists", customersTableExists);
			}

			using (var cmd = new NpgsqlCommand(
				"SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'Registrations')",
				connection))
			{
				var registrationsTableExists = await cmd.ExecuteScalarAsync(cancellationToken);
				data.Add("registrationsTableExists", registrationsTableExists);
			}

			// Query performance
			var startTime = Stopwatch.StartNew();
			using (var cmd = new NpgsqlCommand("SELECT 1", connection))
			{
				await cmd.ExecuteScalarAsync(cancellationToken);
			}
			data.Add("queryResponseTimeMs", startTime.ElapsedMilliseconds);

			return HealthCheckResult.Healthy("PostgreSQL database is healthy and responsive", data);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "PostgreSQL health check failed");
			return HealthCheckResult.Unhealthy("PostgreSQL check failed", ex, data);
		}
	}
}