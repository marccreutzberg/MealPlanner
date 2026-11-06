using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace MealPlanner.Api.Features.Auth;

public static class Logout
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/logout", HandleAsync).RequireAuthorization();

    private static async Task<NoContent> HandleAsync(SignInManager<User> signInManager)
    {
        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }
}
