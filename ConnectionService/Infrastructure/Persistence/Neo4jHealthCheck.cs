using Microsoft.Extensions.Diagnostics.HealthChecks;
using Neo4j.Driver;

namespace ConnectionService.Infrastructure.Persistence;

/// <summary>Readiness: Neo4j accepts connections with the configured credentials.</summary>
public sealed class Neo4jHealthCheck(IDriver driver) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await driver.VerifyConnectivityAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Neo4j is not reachable.", exception);
        }
    }
}
