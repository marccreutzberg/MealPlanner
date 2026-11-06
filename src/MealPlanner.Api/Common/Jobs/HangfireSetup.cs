using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;

namespace MealPlanner.Api.Common.Jobs;

public static class HangfireSetup
{
    public const string DashboardPath = "/hangfire";

    // Cloudflare adds this header to every request through the tunnel; clients cannot remove it.
    private const string CloudflareHeader = "CF-Connecting-IP";

    /// <summary>Hangfire with its job queue in the app database, in its own schema.</summary>
    public static WebApplicationBuilder AddHangfireJobs(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("mealplanner")
            ?? throw new InvalidOperationException("Connection string 'mealplanner' is missing");

        builder.Services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(() => new SqlConnection(connectionString), new SqlServerStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.Zero,
            }));

        builder.Services.AddHangfireServer(o => o.WorkerCount = 2);
        return builder;
    }

    /// <summary>
    /// The dashboard is only for the LAN/WireGuard: requests that came through the Cloudflare tunnel get 404.
    /// Must run before authentication so tunnel visitors are not even sent to the login page.
    /// </summary>
    public static WebApplication UseJobsDashboardTunnelBlock(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments(DashboardPath) && ctx.Request.Headers.ContainsKey(CloudflareHeader))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next(ctx);
        });
        return app;
    }

    /// <summary>
    /// Dashboard for logged-in users only.
    /// Must be called after the database is migrated, because mapping resolves the job storage.
    /// </summary>
    public static WebApplication MapJobsDashboard(this WebApplication app)
    {
        // Hangfire's own filters are cleared (the default only allows localhost); the endpoint policy does the work.
        app.MapHangfireDashboard(DashboardPath, new DashboardOptions
        {
            Authorization = [],
            DashboardTitle = "MealPlanner jobs",
            AppPath = null,
        }).RequireAuthorization();

        return app;
    }
}
