using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>
/// Ticks an item off (or back on). Ticking saves the purchase with store and known price for the buying rhythm.
/// POST /api/shopping-list/items/{id}/bought.
/// </summary>
public static class SetBought
{
    public sealed record Body(bool Bought, int? StoreId = null);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/shopping-list/items/{id:int}/bought", async (int id, Body body, ClaimsPrincipal user,
                Handler handler, CancellationToken ct) =>
            (await handler.HandleAsync(id, body, user.GetHouseholdId(), ct)).ToHttpResult());

    public sealed class Handler(AppDbContext db, TimeProvider clock, ListNotifier notifier)
    {
        public async Task<Result<bool>> HandleAsync(int itemId, Body body, int householdId, CancellationToken ct)
        {
            var item = await db.ShoppingListItems
                .SingleOrDefaultAsync(i => i.Id == itemId && i.ShoppingList.HouseholdId == householdId, ct);
            if (item is null) return Error.NotFound($"Item {itemId} is not on the shopping list");

            if (body.Bought && !item.IsBought)
            {
                var storeId = body.StoreId ?? item.FlaggedStoreId ?? item.PlannedStoreId;
                var offers = await OfferLookup.LoadAsync(db, householdId, DanishTime.Today(clock), ct);
                var price = offers.ForItem(item.IngredientId, item.Name, item.Variant, item.Quantity)
                    .FirstOrDefault(o => storeId is null || o.StoreId == storeId)?.EffectivePrice;
                db.PurchaseHistory.Add(item.MarkBought(householdId, storeId, price, clock.GetUtcNow()));
            }
            else if (!body.Bought && item.IsBought)
            {
                item.MarkNotBought();
            }

            await db.SaveChangesAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);
            return item.IsBought;
        }
    }
}
