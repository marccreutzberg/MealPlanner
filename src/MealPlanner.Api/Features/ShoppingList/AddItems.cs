using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Categories;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>
/// Adds items to the list (spec 4). Category and ingredient are found automatically; an item already on the list
/// gets the amounts combined. Flag order: from an offer, an explicit store, the household's standing choice.
/// POST /api/shopping-list/items and add_to_shopping_list.
/// </summary>
public static class AddItems
{
    public sealed record NewItem(string Name, string? Quantity = null, string? Variant = null, string? Tag = null,
        string? Store = null, int? OfferId = null);

    public sealed record Command(List<NewItem> Items);

    public sealed record Response(List<ShoppingListViews.ItemDto> Items);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/shopping-list/items", async (Command command, ClaimsPrincipal user, Handler handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(command, user.GetHouseholdId(), user.GetUserId(), ct)).ToHttpResult());

    public sealed class Handler(AppDbContext db, TimeProvider clock, ListNotifier notifier)
    {
        private const int MaxItems = 100;

        public async Task<Result<Response>> HandleAsync(Command command, int householdId, string userId, CancellationToken ct)
        {
            if (command.Items is not { Count: > 0 and <= MaxItems })
                return Error.Invalid($"Add between 1 and {MaxItems} items");
            if (command.Items.Any(i => string.IsNullOrWhiteSpace(i.Name) || i.Name.Trim().Length > 200))
                return Error.Invalid("Every item needs a name (max 200 characters)");

            var list = await ShoppingListViews.GetActiveListAsync(db, householdId, ct);
            var stores = await ShoppingListViews.LoadStoresAsync(db, householdId, ct);
            var offers = await OfferLookup.LoadAsync(db, householdId, DanishTime.Today(clock), ct);
            var classifier = await CategoryClassifier.LoadAsync(db, householdId, ct);
            var preferences = (await db.ItemStorePreferences.Where(p => p.HouseholdId == householdId).ToListAsync(ct))
                .ToDictionary(p => p.ItemName, p => p.StoreId);
            var open = await db.ShoppingListItems.Where(i => i.ShoppingListId == list.Id && !i.IsBought).ToListAsync(ct);

            var touched = new List<ShoppingListItem>();
            foreach (var input in command.Items)
            {
                var name = input.Name.Trim();
                var variant = string.IsNullOrWhiteSpace(input.Variant) ? null : input.Variant.Trim();

                // Resolve the flag first so an unknown store or offer fails before anything is changed.
                int? storeId = null;
                int? offerId = null;
                var source = StoreFlagSource.Manual;
                if (input.OfferId is { } requestedOffer)
                {
                    var offer = offers.Find(requestedOffer);
                    if (offer is null) return Error.Invalid($"Offer {requestedOffer} is not valid today");
                    (storeId, offerId, source) = (offer.StoreId, offer.Id, StoreFlagSource.Offer);
                }
                else if (!string.IsNullOrWhiteSpace(input.Store))
                {
                    var store = stores.FirstOrDefault(s => s.Name.Contains(input.Store.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (store is null)
                        return Error.Invalid($"Unknown store '{input.Store}'. Stores: {string.Join(", ", stores.Select(s => s.Name))}");
                    storeId = store.Id;
                }
                else if (preferences.TryGetValue(IngredientMatcher.Normalize(name), out var preferred))
                {
                    (storeId, source) = (preferred, StoreFlagSource.Preference);
                }

                var item = open.FirstOrDefault(i => SameItem(i, name, variant));
                if (item is null)
                {
                    var classification = classifier.Classify(name);
                    item = new ShoppingListItem
                    {
                        ShoppingListId = list.Id,
                        Name = name,
                        Quantity = string.IsNullOrWhiteSpace(input.Quantity) ? null : input.Quantity.Trim(),
                        Variant = variant,
                        CategoryId = classification.CategoryId,
                        IngredientId = classification.IngredientId,
                        AddedByUserId = userId,
                        AddedAt = clock.GetUtcNow(),
                    };
                    db.ShoppingListItems.Add(item);
                    open.Add(item);
                }
                else
                {
                    item.AddQuantity(input.Quantity);
                }

                if (!string.IsNullOrWhiteSpace(input.Tag)) item.Tag = input.Tag.Trim();
                // A standing choice never overrides a flag someone set on purpose.
                if (storeId is { } flagStore && (source != StoreFlagSource.Preference || item.FlaggedStoreId is null))
                    item.FlagTo(flagStore, source, offerId);
                touched.Add(item);
            }

            await db.SaveChangesAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);

            var storeById = stores.ToDictionary(s => s.Id);
            var ingredientNames = await ShoppingListViews.LoadIngredientNamesAsync(db, touched, ct);
            return new Response([.. touched.Distinct().Select(i => ShoppingListViews.ToDto(i, offers, storeById, ingredientNames))]);
        }

        private static bool SameItem(ShoppingListItem item, string name, string? variant) =>
            IngredientMatcher.Normalize(item.Name) == IngredientMatcher.Normalize(name)
            && IngredientMatcher.Normalize(item.Variant ?? "") == IngredientMatcher.Normalize(variant ?? "");
    }
}
