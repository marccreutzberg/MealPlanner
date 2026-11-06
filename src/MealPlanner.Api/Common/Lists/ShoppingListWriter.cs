using MealPlanner.Api.Common.Categories;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Lists;

/// <summary>
/// Puts items on the household's list (spec 4): category and ingredient are found automatically, an item already on
/// the list gets the amounts added up, and the store flag comes from an offer, an explicit store or the household's
/// standing choice. Shared by "add items" and "meal plan to list". Saves, but does not notify.
/// </summary>
public sealed class ShoppingListWriter(AppDbContext db, TimeProvider clock)
{
    private const int MaxItems = 200;

    public sealed record NewItem(string Name, string? Quantity = null, string? Variant = null, string? Tag = null,
        string? Store = null, int? OfferId = null);

    /// <summary>The changed items plus what is needed to show them.</summary>
    public sealed record Written(List<ShoppingListItem> Items, OfferLookup Offers, List<ShoppingListViews.StoreDto> Stores);

    public async Task<Result<Written>> AddAsync(List<NewItem> items, int householdId, string userId, CancellationToken ct)
    {
        if (items is not { Count: > 0 and <= MaxItems })
            return Error.Invalid($"Add between 1 and {MaxItems} items");
        if (items.Any(i => string.IsNullOrWhiteSpace(i.Name) || i.Name.Trim().Length > 200))
            return Error.Invalid("Every item needs a name (max 200 characters)");

        var list = await ShoppingListViews.GetActiveListAsync(db, householdId, ct);
        var stores = await ShoppingListViews.LoadStoresAsync(db, householdId, ct);
        var offers = await OfferLookup.LoadAsync(db, householdId, DanishTime.Today(clock), ct);
        var classifier = await CategoryClassifier.LoadAsync(db, householdId, ct);
        var preferences = (await db.ItemStorePreferences.Where(p => p.HouseholdId == householdId).ToListAsync(ct))
            .ToDictionary(p => p.ItemName, p => p.StoreId);
        var open = await db.ShoppingListItems.Where(i => i.ShoppingListId == list.Id && !i.IsBought).ToListAsync(ct);

        var touched = new List<ShoppingListItem>();
        foreach (var input in items)
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
            if (!touched.Contains(item)) touched.Add(item);
        }

        await db.SaveChangesAsync(ct);
        return new Written(touched, offers, stores);
    }

    private static bool SameItem(ShoppingListItem item, string name, string? variant) =>
        IngredientMatcher.Normalize(item.Name) == IngredientMatcher.Normalize(name)
        && IngredientMatcher.Normalize(item.Variant ?? "") == IngredientMatcher.Normalize(variant ?? "");
}
