using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>The household's list grouped by category with offer badges. GET /api/shopping-list and get_shopping_list.</summary>
public static class GetList
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/shopping-list", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(user.GetHouseholdId(), ct)));

    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        public Task<ShoppingListViews.ListDto> HandleAsync(int householdId, CancellationToken ct) =>
            ShoppingListViews.LoadAsync(db, householdId, clock, ct);
    }
}
