using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace MealPlanner.Api.Features.Auth;

/// <summary>JSON login for the Angular app. Sets the auth cookie.</summary>
public static class Login
{
    public sealed record Request(string Email, string Password, bool RememberMe = true);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login", HandleAsync).AllowAnonymous();

    private static async Task<Results<NoContent, UnauthorizedHttpResult, StatusCodeHttpResult>> HandleAsync(
        Request request, SignInManager<User> signInManager)
    {
        var result = await signInManager.PasswordSignInAsync(request.Email, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return TypedResults.NoContent();
        if (result.IsLockedOut) return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
        return TypedResults.Unauthorized();
    }
}
