using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Propel.FeatureFlags.Dashboard.Api.Healthchecks;

public static class HealthCheckExtensions
{
    public static void MapHealthCheckEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness probe - returns 200 if the application is running
        app.MapHealthChecks("api/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("liveness"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description,
                        duration = entry.Value.Duration.TotalMilliseconds
                    }),
                    timestamp = DateTime.UtcNow
                };
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        });

        // Readiness probe - returns 200 if the application is ready to handle requests
        app.MapHealthChecks("api/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("readiness"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                
                // Determine overall status
                var overallStatus = "Healthy";
                if (report.Status == HealthStatus.Degraded)
                    overallStatus = "Degraded";
                else if (report.Status == HealthStatus.Unhealthy)
                    overallStatus = "Unhealthy";

                var response = new
                {
                    status = overallStatus,
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description,
                        duration = entry.Value.Duration.TotalMilliseconds,
                        exception = entry.Value.Exception?.Message
                    }),
                    timestamp = DateTime.UtcNow
                };

                // Set appropriate status code based on health check results
                context.Response.StatusCode = report.Status switch
                {
                    HealthStatus.Healthy => 200,
                    HealthStatus.Degraded => 200, // Still ready but degraded
                    HealthStatus.Unhealthy => 503, // Service unavailable
                    _ => 500
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        });

        // General health endpoint with detailed information
        app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    status = report.Status.ToString(),
                    totalDuration = report.TotalDuration.TotalMilliseconds,
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description,
                        duration = entry.Value.Duration.TotalMilliseconds,
                        exception = entry.Value.Exception?.Message,
                        tags = entry.Value.Tags
                    }),
                    timestamp = DateTime.UtcNow
                };

                context.Response.StatusCode = report.Status switch
                {
                    HealthStatus.Healthy => 200,
                    HealthStatus.Degraded => 200,
                    HealthStatus.Unhealthy => 503,
                    _ => 500
                };

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        });
    }
}