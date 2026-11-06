using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Data;

public static class DatabaseStartup
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private const int MaxAttempts = 24;

    /// <summary>
    /// Applies pending migrations (which also runs the EF seeding hook) before anything else touches the database.
    /// Retries for about two minutes because the SQL container may still be starting.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseStartup));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Database migrated and seeded");
                return;
            }
            catch (SqlException ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, MaxAttempts, ex.Message);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }
}
