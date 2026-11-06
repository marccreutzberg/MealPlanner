using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.ApiKeys;

public static class ListApiKeys
{
    public sealed record Item(int Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/api-keys", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
            {
                var userId = user.GetUserId();
                return TypedResults.Ok(await db.ApiKeys
                    .Where(k => k.UserId == userId)
                    .OrderByDescending(k => k.CreatedAt)
                    .Select(k => new Item(k.Id, k.Name, k.CreatedAt, k.RevokedAt))
                    .ToListAsync(ct));
            })
            .RequireAuthorization(Policies.Cookie);
}
