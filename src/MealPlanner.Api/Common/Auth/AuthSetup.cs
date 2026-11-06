using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

namespace MealPlanner.Api.Common.Auth;

public static class AuthSetup
{
    public const string LoginPath = "/account/login";

    public static WebApplicationBuilder AddCookieAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        // Keys live in the database so cookies survive container rebuilds.
        services.AddDataProtection()
            .SetApplicationName("MealPlanner")
            .PersistKeysToDbContext<AppDbContext>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddAuthorization();

        // No open registration: users come from configuration (see UserSeeder).
        services.AddIdentityCore<User>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
                o.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

        services.ConfigureApplicationCookie(o =>
        {
            o.Cookie.Name = "MealPlanner.Auth";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromDays(30);
            o.SlidingExpiration = true;
            o.LoginPath = LoginPath;
            // API callers get status codes, browsers on other pages get the login page.
            o.Events.OnRedirectToLogin = ctx => RedirectOrStatus(ctx, StatusCodes.Status401Unauthorized);
            o.Events.OnRedirectToAccessDenied = ctx => RedirectOrStatus(ctx, StatusCodes.Status403Forbidden);
        });

        // Cloudflare Tunnel terminates HTTPS; trust its X-Forwarded-Proto so cookies are marked Secure.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddLocalization();
        services.AddAntiforgery();
        services.AddHostedService<UserSeeder>();

        return builder;
    }

    private static Task RedirectOrStatus(Microsoft.AspNetCore.Authentication.RedirectContext<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions> ctx, int statusCode)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = statusCode;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }
}
