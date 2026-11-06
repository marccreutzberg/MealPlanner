using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Lists;

/// <summary>
/// The shopping list as the app and Claude see it: items with category, store flag and offer badge.
/// Shared by the shopping list slices and MCP tools.
/// </summary>
public static class ShoppingListViews
{
    // Data, not UI text: the name of the list created for a household.
    public const string DefaultListName = "Indkøbsliste";

    public sealed record OfferBadge(int OfferId, string Store, string Heading, decimal Price, DateOnly LastValidDay,
        bool RequiresAppOrMembership);

    public sealed record StoreFlag(int StoreId, string Store, StoreFlagSource Source, DateOnly? LastValidDay);

    public sealed record ItemDto(int Id, string Name, string? Quantity, string? Variant, string? Tag, int? CategoryId,
        string? Ingredient, bool IsBought, StoreFlag? Flag, OfferBadge? BestOffer, int OfferCount);

    public sealed record CategoryDto(int Id, string Name, string Color, List<ItemDto> Items);

    public sealed record StoreDto(int Id, string Name, string Color, bool IsHomeStore, string? StopGroup);

    public sealed record CategoryRef(int Id, string Name, string Color);

    /// <param name="Categories">Categories that have items, with the items.</param>
    /// <param name="AllCategories">Every category, for moving an item.</param>
    public sealed record ListDto(int Id, string Name, List<CategoryDto> Categories, List<StoreDto> Stores,
        List<CategoryRef> AllCategories);

    /// <summary>The household's one active list, created on first use.</summary>
    public static async Task<ShoppingList> GetActiveListAsync(AppDbContext db, int householdId, CancellationToken ct)
    {
        var list = await db.ShoppingLists.FirstOrDefaultAsync(l => l.HouseholdId == householdId && l.IsActive, ct);
        if (list is not null) return list;

        list = new ShoppingList { HouseholdId = householdId, Name = DefaultListName };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync(ct);
        return list;
    }

    public static async Task<List<StoreDto>> LoadStoresAsync(AppDbContext db, int householdId, CancellationToken ct) =>
        await db.HouseholdStores
            .Where(h => h.HouseholdId == householdId && h.IsActive)
            .OrderByDescending(h => h.IsHomeStore).ThenBy(h => h.Store.Name)
            .Select(h => new StoreDto(h.StoreId, h.Store.Name, h.Store.Color, h.IsHomeStore, h.StopGroup))
            .ToListAsync(ct);

    public static ItemDto ToDto(ShoppingListItem item, OfferLookup offers, IReadOnlyDictionary<int, StoreDto> stores,
        IReadOnlyDictionary<int, string> ingredientNames)
    {
        var candidates = offers.ForItem(item.IngredientId, item.Name, item.Variant);
        var flaggedOffer = item.OfferId is { } offerId ? offers.Find(offerId) : null;
        var best = item.FlaggedStoreId is { } flagged
            ? flaggedOffer ?? candidates.FirstOrDefault(o => o.StoreId == flagged)
            : candidates.FirstOrDefault();

        StoreFlag? flag = item.FlaggedStoreId is { } storeId && stores.TryGetValue(storeId, out var store)
            ? new StoreFlag(storeId, store.Name, item.FlagSource ?? StoreFlagSource.Manual, flaggedOffer?.LastValidDay)
            : null;

        return new ItemDto(item.Id, item.Name, item.Quantity, item.Variant, item.Tag, item.CategoryId,
            item.IngredientId is { } ing ? ingredientNames.GetValueOrDefault(ing) : null, item.IsBought, flag,
            best is null ? null : new OfferBadge(best.Id, best.StoreName, best.Heading, best.Price, best.LastValidDay,
                best.RequiresAppOrMembership),
            candidates.Count);
    }

    /// <summary>The whole list grouped by category in the home store's walking order.</summary>
    public static async Task<ListDto> LoadAsync(AppDbContext db, int householdId, TimeProvider clock, CancellationToken ct)
    {
        var list = await GetActiveListAsync(db, householdId, ct);
        var items = await db.ShoppingListItems.Where(i => i.ShoppingListId == list.Id).OrderBy(i => i.AddedAt).ToListAsync(ct);
        var categories = await db.Categories.OrderBy(c => c.SortOrder).ToListAsync(ct);
        var stores = await LoadStoresAsync(db, householdId, ct);
        var offers = await OfferLookup.LoadAsync(db, householdId, DanishTime.Today(clock), ct);
        var ingredientNames = await LoadIngredientNamesAsync(db, items, ct);
        var storeById = stores.ToDictionary(s => s.Id);
        var fallback = categories.Last().Id;

        var dtos = items.Select(i => ToDto(i, offers, storeById, ingredientNames)).ToList();
        return new ListDto(list.Id, list.Name,
            [.. categories
                .Select(c => new CategoryDto(c.Id, c.Name, c.Color, [.. dtos.Where(d => (d.CategoryId ?? fallback) == c.Id)]))
                .Where(c => c.Items.Count > 0)],
            stores,
            [.. categories.Select(c => new CategoryRef(c.Id, c.Name, c.Color))]);
    }

    public static async Task<Dictionary<int, string>> LoadIngredientNamesAsync(AppDbContext db,
        IEnumerable<ShoppingListItem> items, CancellationToken ct)
    {
        var ids = items.Where(i => i.IngredientId != null).Select(i => i.IngredientId!.Value).Distinct().ToList();
        return await db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
    }
}
