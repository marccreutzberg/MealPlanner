using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>
/// Ticked items, most recently bought first, one page at a time. GET /api/shopping-list/bought?skip=0&amp;take=20.
/// Offers are left out: they no longer matter once the item is bought.
/// </summary>
public static class GetBought
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public sealed record Page(List<ShoppingListViews.ItemDto> Items, int Total);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/shopping-list/bought", async (ClaimsPrincipal user, Handler handler, CancellationToken ct,
                int skip = 0, int take = DefaultPageSize) =>
            TypedResults.Ok(await handler.HandleAsync(user.GetHouseholdId(), skip, take, ct)));

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Page> HandleAsync(int householdId, int skip, int take, CancellationToken ct)
        {
            skip = Math.Max(skip, 0);
            take = Math.Clamp(take, 1, MaxPageSize);

            var list = await ShoppingListViews.GetActiveListAsync(db, householdId, ct);
            var bought = db.ShoppingListItems.Where(i => i.ShoppingListId == list.Id && i.IsBought);
            var total = await bought.CountAsync(ct);
            var items = await bought
                .OrderByDescending(i => i.BoughtAt).ThenByDescending(i => i.Id)
                .Skip(skip).Take(take)
                .ToListAsync(ct);

            var stores = (await ShoppingListViews.LoadStoresAsync(db, householdId, ct)).ToDictionary(s => s.Id);
            var ingredientNames = await ShoppingListViews.LoadIngredientNamesAsync(db, items, ct);
            return new Page([.. items.Select(i => ToDto(i, stores, ingredientNames))], total);
        }

        private static ShoppingListViews.ItemDto ToDto(ShoppingListItem item,
            IReadOnlyDictionary<int, ShoppingListViews.StoreDto> stores, IReadOnlyDictionary<int, string> ingredientNames)
        {
            var flag = item.FlaggedStoreId is { } storeId && stores.TryGetValue(storeId, out var store)
                ? new ShoppingListViews.StoreFlag(storeId, store.Name, item.FlagSource ?? StoreFlagSource.Manual, null)
                : null;
            return new ShoppingListViews.ItemDto(item.Id, item.Name, item.Quantity, item.Variant, item.Tag,
                item.CategoryId, item.IngredientId is { } ing ? ingredientNames.GetValueOrDefault(ing) : null,
                item.IsBought, flag, BestOffer: null, OfferCount: 0, IsPinned: false);
        }
    }
}
