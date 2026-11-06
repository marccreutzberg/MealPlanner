using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Trips;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>Deletes one item, or clears all bought items. Items on a trip are frozen there first.</summary>
public static class RemoveItems
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/shopping-list/items/{id:int}", async Task<Results<NoContent, NotFound>> (int id,
            ClaimsPrincipal user, AppDbContext db, ListNotifier notifier, CancellationToken ct) =>
        {
            var householdId = user.GetHouseholdId();
            var ids = await db.ShoppingListItems.Where(i => i.Id == id && i.ShoppingList.HouseholdId == householdId)
                .Select(i => i.Id).ToListAsync(ct);
            await TripFreezer.FreezeAsync(db, ids, ct);
            var deleted = await db.ShoppingListItems.Where(i => ids.Contains(i.Id)).ExecuteDeleteAsync(ct);
            if (deleted == 0) return TypedResults.NotFound();
            await notifier.ListChangedAsync(householdId, ct);
            return TypedResults.NoContent();
        });

        app.MapPost("/api/shopping-list/clear-bought", async (ClaimsPrincipal user, AppDbContext db, ListNotifier notifier,
            CancellationToken ct) =>
        {
            var householdId = user.GetHouseholdId();
            // The ids are read once: an item ticked on the other phone meanwhile is left for next time.
            var ids = await db.ShoppingListItems
                .Where(i => i.IsBought && i.ShoppingList.HouseholdId == householdId && i.ShoppingList.IsActive)
                .Select(i => i.Id).ToListAsync(ct);
            await TripFreezer.FreezeAsync(db, ids, ct);
            var deleted = await db.ShoppingListItems.Where(i => ids.Contains(i.Id)).ExecuteDeleteAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);
            return TypedResults.Ok(new { Deleted = deleted });
        });
    }
}
