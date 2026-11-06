using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MealPlanner.Api.Common.Trips;

/// <summary>A trip as the app and Claude see it. Shared by the trip slices and MCP tools.</summary>
public static class TripViews
{
    private const int NewItemNames = 3;

    /// <param name="ItemId">The list item, for ticking; null when it was removed from the list or the trip is finished.</param>
    public sealed record TripItemDto(int Id, int? ItemId, string Name, string? Variant, string? Quantity, string? Tag,
        bool IsBought, bool MovedByHand, string? OfferHeading, decimal? UnitPrice, int Count, decimal? LinePrice,
        DateOnly? LastValidDay);

    public sealed record VisitDto(int StoreId, string Store, string Color, bool IsHomeStore, decimal Subtotal,
        List<TripItemDto> Items);

    public sealed record StoreOptionDto(int StoreId, string Name, string Color, bool IsHomeStore, string? StopGroup, bool OnTrip);

    /// <param name="Names">The first few names of list items not on the trip yet.</param>
    public sealed record NewItemsDto(int Count, List<string> Names);

    public sealed record TripDto(int Id, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, decimal EstimatedSaving,
        List<VisitDto> Visits, List<StoreOptionDto> Stores, NewItemsDto NewItems, string Text);

    public sealed record TripSummaryDto(int Id, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, List<string> Stores,
        int ItemCount, int BoughtCount, decimal Total);

    /// <summary>
    /// One trip item. On an active trip it shows the list item as it is now (one tick, one amount); a removed item
    /// is shown only if it was bought. A finished trip shows the frozen copy.
    /// </summary>
    public static TripItemDto? ToItem(ShoppingTripItem item, bool active)
    {
        var live = active ? item.ShoppingListItem : null;
        if (active && live is null && !item.WasBought) return null;
        if (live is null)
            return new TripItemDto(item.Id, null, item.Name, item.Variant, item.Quantity, item.Tag, item.WasBought,
                item.MovedByHand, item.OfferHeading, item.UnitPrice, item.Count, item.LinePrice, item.LastValidDay);

        // The amount may have changed on the list since the trip was made ("2 pk." + "2 pk." = "4 pk.").
        var count = StoreSelector.PieceCount(live.Quantity);
        return new TripItemDto(item.Id, live.Id, live.Name, live.Variant, live.Quantity, live.Tag, live.IsBought,
            item.MovedByHand, item.OfferHeading, item.UnitPrice, count, item.UnitPrice * count, item.LastValidDay);
    }

    /// <summary>Open items on the household's list that are not on the trip.</summary>
    public static IQueryable<ShoppingListItem> NewItems(AppDbContext db, int listId, int tripId) =>
        db.ShoppingListItems.Where(i => i.ShoppingListId == listId && !i.IsBought
            && !db.ShoppingTripItems.Any(t => t.TripId == tripId && t.ShoppingListItemId == i.Id));

    /// <summary>The trip with items per store in driving order (other stores by basket size, home store last).</summary>
    public static async Task<TripDto> LoadAsync(AppDbContext db, ShoppingTrip trip, IStringLocalizer<TripTexts> texts,
        CancellationToken ct)
    {
        var householdStores = await ShoppingListViews.LoadStoresAsync(db, trip.HouseholdId, ct);
        // A finished trip may name stores the household no longer uses.
        var storeIds = trip.Items.Select(i => i.StoreId).Distinct().ToList();
        var allStores = await db.Stores.Where(s => storeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (s.Name, s.Color), ct);
        foreach (var s in householdStores) allStores[s.Id] = (s.Name, s.Color);
        var homeId = householdStores.FirstOrDefault(s => s.IsHomeStore)?.Id;

        var items = trip.Items
            .Select(i => (Item: i, Dto: ToItem(i, trip.IsActive)))
            .Where(x => x.Dto is not null)
            .ToList();
        var visitStoreIds = items.Select(x => x.Item.StoreId).ToHashSet();
        if (trip.IsActive && homeId is { } home) visitStoreIds.Add(home);

        var visits = visitStoreIds
            .Select(id =>
            {
                var here = items.Where(x => x.Item.StoreId == id).Select(x => x.Dto!).ToList();
                return new VisitDto(id, allStores[id].Name, allStores[id].Color, id == homeId,
                    here.Sum(i => i.LinePrice ?? 0), here);
            })
            .OrderBy(v => v.IsHomeStore)
            .ThenByDescending(v => v.Subtotal)
            .ToList();

        var onTrip = trip.Stores.Select(s => s.StoreId).ToHashSet();
        var options = householdStores
            .Select(s => new StoreOptionDto(s.Id, s.Name, s.Color, s.IsHomeStore, s.StopGroup, onTrip.Contains(s.Id) || s.IsHomeStore))
            .ToList();

        var newItems = new NewItemsDto(0, []);
        if (trip.IsActive)
        {
            var list = await ShoppingListViews.GetActiveListAsync(db, trip.HouseholdId, ct);
            var query = NewItems(db, list.Id, trip.Id);
            newItems = new NewItemsDto(await query.CountAsync(ct),
                await query.OrderBy(i => i.AddedAt).Select(i => i.Name).Take(NewItemNames).ToListAsync(ct));
        }

        return new TripDto(trip.Id, trip.CreatedAt, trip.FinishedAt, trip.EstimatedSaving, visits, options, newItems,
            TripText.Format(ToPlan(visits), texts));
    }

    public static TripSummaryDto ToSummary(ShoppingTrip trip, IReadOnlyDictionary<int, string> storeNames) =>
        new(trip.Id, trip.CreatedAt, trip.FinishedAt,
            [.. trip.Items.Select(i => i.StoreId).Distinct().Select(id => storeNames.GetValueOrDefault(id, ""))],
            trip.Items.Count, trip.Items.Count(i => i.WasBought), trip.Items.Sum(i => i.LinePrice ?? 0));

    /// <summary>The saved trip in the planner's shape, so the text is formatted exactly like before.</summary>
    private static StoreSelector.Plan ToPlan(List<VisitDto> visits) =>
        new([.. visits.Select(v => new StoreSelector.Visit(
                new StoreSelector.Store(v.StoreId, v.Store, v.IsHomeStore, null),
                [.. v.Items.Select(i => new StoreSelector.PlannedItem(
                    new StoreSelector.Item(i.Id, i.Name, i.Variant, i.Quantity, i.Tag, null, i.IsBought, []),
                    i.UnitPrice is { } price
                        ? new StoreSelector.Offer(0, v.StoreId, i.OfferHeading ?? "", price, null, false, i.LastValidDay ?? default)
                        : null,
                    i.Count, i.LinePrice))]))],
            0);
}
