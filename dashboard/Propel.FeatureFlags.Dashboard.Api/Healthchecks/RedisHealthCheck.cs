using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using System.Diagnostics;

namespace Propel.FeatureFlags.Dashboard.Api.Healthchecks;

/// <summary>
/// Deep health check for Redis that verifies connection, basic operations, and server info
/// </summary>
public class RedisDeepHealthCheck(IConfiguration configuration, ILogger<RedisDeepHealthCheck> logger) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		var connectionString = configuration.GetConnectionString("RedisCache");
		if (string.IsNullOrEmpty(connectionString))
		{
			return HealthCheckResult.Degraded("Redis connection string is missing");
		}

		var data = new Dictionary<string, object>();
		var healthCheckKey = $"health:check:{Guid.NewGuid()}";
		var healthCheckValue = DateTimeOffset.UtcNow.ToString("o");

		try
		{
			var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
			data.Add("connected", connection.IsConnected);

			var db = connection.GetDatabase();

			// Test PING command
			var pingTime = Stopwatch.StartNew();
			await db.PingAsync();
			data.Add("pingTimeMs", pingTime.ElapsedMilliseconds);

			// Test basic ECHO command
			var echoResult = await db.ExecuteAsync("ECHO", "HELLO");
			data.Add("echoTest", echoResult.ToString() == "HELLO");

			// Test SET and GET operations
			await db.StringSetAsync(healthCheckKey, healthCheckValue, TimeSpan.FromSeconds(10));
			var retrievedValue = await db.StringGetAsync(healthCheckKey);
			data.Add("setGetWorking", retrievedValue == healthCheckValue);

			// Get server information
			var server = connection.GetServer(connection.GetEndPoints()[0]);
			var info = await server.InfoAsync();

			var serverInfo = new Dictionary<string, string>();
			foreach (var group in info)
			{
				foreach (var item in group)
				{
					serverInfo[item.Key] = item.Value;
				}
			}

			// Add key server metrics
			data.Add("serverInfo", new
			{
				version = serverInfo.GetValueOrDefault("redis_version", "unknown"),
				uptime = serverInfo.GetValueOrDefault("uptime_in_seconds", "unknown"),
				connectedClients = serverInfo.GetValueOrDefault("connected_clients", "unknown"),
				usedMemory = serverInfo.GetValueOrDefault("used_memory_human", "unknown"),
				totalKeys = serverInfo.GetValueOrDefault("db0", "unknown")
			});

			return HealthCheckResult.Healthy("Redis is healthy and responsive", data);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Redis health check failed");
			return HealthCheckResult.Degraded("Redis check failed", ex, data);
		}
	}
}