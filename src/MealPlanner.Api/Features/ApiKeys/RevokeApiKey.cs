using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.ApiKeys;

/// <summary>Revokes one of the signed-in user's keys. The row stays for history.</summary>
public static class RevokeApiKey
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/api-keys/{id:int}", async Task<Results<NoContent, NotFound>> (int id, ClaimsPrincipal user,
                AppDbContext db, TimeProvider clock, CancellationToken ct) =>
            {
                var userId = user.GetUserId();
                var key = await db.ApiKeys.SingleOrDefaultAsync(k => k.Id == id && k.UserId == userId, ct);
                if (key is null) return TypedResults.NotFound();

                key.Revoke(clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(Policies.Cookie);
}
