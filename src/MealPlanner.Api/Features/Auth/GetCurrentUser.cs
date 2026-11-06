using System.Security.Claims;
using MealPlanner.Api.Common.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Auth;

public static class GetCurrentUser
{
    public sealed record Response(string Email, string DisplayName, int HouseholdId, string HouseholdName);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/auth/me", HandleAsync).RequireAuthorization();

    private static async Task<Results<Ok<Response>, UnauthorizedHttpResult>> HandleAsync(
        ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new Response(u.Email!, u.DisplayName, u.HouseholdId, u.Household.Name))
            .SingleOrDefaultAsync(ct);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(user);
    }
}
