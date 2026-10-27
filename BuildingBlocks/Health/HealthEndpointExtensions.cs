using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Health;

/// <summary>
/// /health        liveness: the process is running (no dependency checks; used to restart a hung container).
/// /health/ready  readiness: dependencies such as the database or Neo4j answer (used before sending traffic).
/// Both are anonymous and return a small JSON report.
/// </summary>
public static class HealthEndpointExtensions
{
    public const string ReadyTag = "ready";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteAsync });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag), ResponseWriter = WriteAsync });
        return app;
    }

    private static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            durationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(entry => entry.Key, entry => new
            {
                status = entry.Value.Status.ToString(),
                error = entry.Value.Exception?.Message
            })
        }, JsonOptions));
    }
}
