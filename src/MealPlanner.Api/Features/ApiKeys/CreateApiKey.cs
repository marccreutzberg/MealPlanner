using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MealPlanner.Api.Features.ApiKeys;

/// <summary>Creates an API key for the signed-in user. The plain key is returned once and never stored.</summary>
public static class CreateApiKey
{
    public sealed record Command(string Name);

    public sealed record Response(int Id, string Name, string Key, DateTimeOffset CreatedAt);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/api-keys", async Task<Results<Ok<Response>, ProblemHttpResult>> (Command command,
                ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
            {
                var name = command.Name?.Trim() ?? "";
                if (name.Length is 0 or > 100)
                    return TypedResults.Problem("name is required (max 100 characters)", statusCode: StatusCodes.Status400BadRequest);

                var key = ApiKeyGenerator.Generate();
                var apiKey = new ApiKey
                {
                    UserId = user.GetUserId(),
                    HouseholdId = user.GetHouseholdId(),
                    Name = name,
                    KeyHash = ApiKeyGenerator.Hash(key),
                    CreatedAt = clock.GetUtcNow(),
                };
                db.ApiKeys.Add(apiKey);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new Response(apiKey.Id, apiKey.Name, key, apiKey.CreatedAt));
            })
            // A key must never be able to create more keys.
            .RequireAuthorization(Policies.Cookie);
}
