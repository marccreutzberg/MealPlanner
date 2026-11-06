namespace MealPlanner.Api.Common.Web;

/// <summary>
/// Serves the Angular PWA from wwwroot (built by web/ into it). Unknown paths get index.html so deep links work,
/// except server paths, which keep their real 404.
/// </summary>
public static class SpaHosting
{
    private static readonly string[] ServerPaths = ["/api", "/mcp", "/hubs", "/hangfire", "/account", "/health", "/alive"];

    public static WebApplication UseSpaFiles(this WebApplication app)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            // index.html and the service worker must always be fresh; hashed bundles may be cached.
            OnPrepareResponse = ctx =>
            {
                var name = ctx.File.Name;
                if (name is "index.html" or "ngsw-worker.js" or "ngsw.json" or "manifest.webmanifest")
                    ctx.Context.Response.Headers.CacheControl = "no-cache";
            },
        });
        return app;
    }

    public static WebApplication MapSpaFallback(this WebApplication app)
    {
        app.MapFallback(async context =>
        {
            var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
            if (ServerPaths.Any(p => context.Request.Path.StartsWithSegments(p)) || !index.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(index);
        });
        return app;
    }
}
