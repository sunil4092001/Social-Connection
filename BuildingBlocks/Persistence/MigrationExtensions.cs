using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations at startup when Database:MigrateOnStartup is true (docker-compose sets it,
    /// so a fresh stack creates its own schemas). Local development keeps using `dotnet ef database update`.
    /// Retries for a short while because a database container may still be starting.
    /// </summary>
    public static async Task MigrateDatabaseIfConfiguredAsync<TContext>(this WebApplication app) where TContext : DbContext
    {
        if (!app.Configuration.GetValue("Database:MigrateOnStartup", false)) return;

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<TContext>().Database.MigrateAsync();
                logger.LogInformation("Database for {Context} is up to date", typeof(TContext).Name);
                return;
            }
            catch (Exception exception) when (attempt < 10)
            {
                logger.LogWarning(exception, "Migration attempt {Attempt} for {Context} failed; retrying", attempt, typeof(TContext).Name);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
