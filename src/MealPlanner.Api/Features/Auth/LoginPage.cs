using System.Text.Encodings.Web;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace MealPlanner.Api.Features.Auth;

/// <summary>
/// Minimal server-rendered login so the Hangfire dashboard can be reached before the Angular app exists.
/// Texts come from LoginPage.resx.
/// </summary>
public static class LoginPage
{
    private const string DefaultReturnUrl = "/hangfire";

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(AuthSetup.LoginPath, Render).AllowAnonymous().ExcludeFromDescription();
        app.MapPost(AuthSetup.LoginPath, SubmitAsync).AllowAnonymous().ExcludeFromDescription();
    }

    private static IResult Render(HttpContext http, IAntiforgery antiforgery, IStringLocalizer<LoginPageTexts> texts,
        string? returnUrl) =>
        Page(http, antiforgery, texts, returnUrl, email: null, error: null);

    private static async Task<IResult> SubmitAsync(HttpContext http, IAntiforgery antiforgery,
        IStringLocalizer<LoginPageTexts> texts, SignInManager<User> signInManager,
        [FromForm] string email, [FromForm] string password, [FromQuery] string? returnUrl)
    {
        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);
        if (result.Succeeded)
            return Results.LocalRedirect(IsLocal(returnUrl) ? returnUrl! : DefaultReturnUrl);

        var error = result.IsLockedOut ? texts["LockedOut"] : texts["InvalidLogin"];
        return Page(http, antiforgery, texts, returnUrl, email, error);
    }

    private static bool IsLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");

    private static IResult Page(HttpContext http, IAntiforgery antiforgery, IStringLocalizer<LoginPageTexts> texts,
        string? returnUrl, string? email, string? error)
    {
        var tokens = antiforgery.GetAndStoreTokens(http);
        var e = HtmlEncoder.Default;
        var action = AuthSetup.LoginPath + (IsLocal(returnUrl) ? "?returnUrl=" + UrlEncoder.Default.Encode(returnUrl!) : "");
        var errorHtml = error is null ? "" : $"""<p class="error">{e.Encode(error)}</p>""";

        var html = $$"""
            <!doctype html>
            <html lang="da">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{e.Encode(texts["Title"])}} · MealPlanner</title>
              <style>
                body { font-family: system-ui, sans-serif; background: #f4f5f7; margin: 0; display: grid; place-items: center; min-height: 100vh; }
                form { background: #fff; padding: 2rem; border-radius: 12px; box-shadow: 0 2px 12px rgb(0 0 0 / .08); width: min(360px, calc(100vw - 32px)); box-sizing: border-box; }
                h1 { margin: 0 0 1.5rem; font-size: 1.4rem; }
                label { display: block; margin: 0 0 1rem; font-size: .9rem; }
                input { display: block; width: 100%; box-sizing: border-box; margin-top: .3rem; padding: .6rem; font-size: 1rem; border: 1px solid #c8ccd2; border-radius: 6px; }
                button { width: 100%; padding: .7rem; font-size: 1rem; border: 0; border-radius: 6px; background: #0a2d6e; color: #fff; cursor: pointer; }
                .error { color: #b3261e; margin: 0 0 1rem; }
              </style>
            </head>
            <body>
              <form method="post" action="{{e.Encode(action)}}">
                <h1>{{e.Encode(texts["Title"])}}</h1>
                {{errorHtml}}
                <input type="hidden" name="{{e.Encode(tokens.FormFieldName)}}" value="{{e.Encode(tokens.RequestToken!)}}">
                <label>{{e.Encode(texts["Email"])}}<input type="email" name="email" autocomplete="username" required value="{{e.Encode(email ?? "")}}"></label>
                <label>{{e.Encode(texts["Password"])}}<input type="password" name="password" autocomplete="current-password" required></label>
                <button type="submit">{{e.Encode(texts["Submit"])}}</button>
              </form>
            </body>
            </html>
            """;
        return Results.Content(html, "text/html; charset=utf-8");
    }
}
