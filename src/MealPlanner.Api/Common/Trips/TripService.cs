using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Trips;

/// <summary>
/// Creates and changes saved trips (design 5.1–5.5). Every change uses today's offers and the same store rules as
/// <see cref="StoreSelector"/>, and keeps the list items' planned store in step. Saves, but does not notify.
/// </summary>
public sealed class TripService(AppDbContext db, TimeProvider clock)
{
    private sealed record Context(List<StoreSelector.Store> Stores, OfferLookup Offers)
    {
        public int HomeId => Stores.First(s => s.IsHome).Id;
    }

    private IQueryable<ShoppingTrip> Trips(int householdId) => db.ShoppingTrips
        .Where(t => t.HouseholdId == householdId)
        .Include(t => t.Stores)
        .Include(t => t.Items).ThenInclude(i => i.ShoppingListItem)
        .AsSplitQuery();

    public Task<ShoppingTrip?> FindAsync(int householdId, int tripId, CancellationToken ct) =>
        Trips(householdId).FirstOrDefaultAsync(t => t.Id == tripId, ct);

    public Task<ShoppingTrip?> FindActiveAsync(int householdId, CancellationToken ct) =>
        Trips(householdId).FirstOrDefaultAsync(t => t.FinishedAt == null, ct);

    /// <summary>The trip, if it belongs to the household and can still be changed.</summary>
    public async Task<Result<ShoppingTrip>> FindEditableAsync(int householdId, int tripId, CancellationToken ct)
    {
        var trip = await FindAsync(householdId, tripId, ct);
        if (trip is null) return Error.NotFound($"Trip {tripId} not found");
        if (!trip.IsActive) return Error.Invalid("The trip is finished");
        return trip;
    }

    /// <summary>A trip with everything not bought yet. Finishes the active trip first. No stores given = the planner chooses.</summary>
    public async Task<ShoppingTrip> CreateAsync(int householdId, string? userId, IReadOnlyCollection<int>? storeIds,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (await FindActiveAsync(householdId, ct) is { } active)
        {
            active.Finish(now);
            await db.SaveChangesAsync(ct); // before the new one: one active trip per household is a unique index
        }

        var context = await LoadContextAsync(householdId, ct);
        var list = await ShoppingListViews.GetActiveListAsync(db, householdId, ct);
        var open = await db.ShoppingListItems.Where(i => i.ShoppingListId == list.Id && !i.IsBought)
            .OrderBy(i => i.AddedAt).ToListAsync(ct);
        var plannerItems = open.Select(i => TripPlanner.ToPlannerItem(i, context.Offers, null)).ToList();

        StoreSelector.Plan plan;
        List<int> tripStores;
        if (storeIds is null)
        {
            var threshold = await db.Households.Where(h => h.Id == householdId).Select(h => h.ThirdStoreThreshold).SingleAsync(ct);
            plan = StoreSelector.Choose(context.Stores, plannerItems, threshold);
            tripStores = [.. plan.Visits.Select(v => v.Store.Id)];
        }
        else
        {
            plan = StoreSelector.Distribute(context.Stores, storeIds, plannerItems);
            tripStores = [.. context.Stores.Where(s => s.IsHome || storeIds.Contains(s.Id)).Select(s => s.Id)];
        }

        var trip = new ShoppingTrip
        {
            HouseholdId = householdId, CreatedAt = now, CreatedByUserId = userId, EstimatedSaving = plan.EstimatedSaving,
            Stores = [.. tripStores.Distinct().Select(id => new ShoppingTripStore { StoreId = id })],
        };
        var byId = open.ToDictionary(i => i.Id);
        foreach (var visit in plan.Visits)
            foreach (var planned in visit.Items)
            {
                var listItem = byId[planned.Item.Id];
                var tripItem = new ShoppingTripItem
                {
                    ShoppingListItem = listItem, Name = listItem.Name, Quantity = listItem.Quantity,
                    Variant = listItem.Variant, Tag = listItem.Tag,
                };
                TripPlanner.Apply(tripItem, planned, visit.Store.Id, byHand: false);
                listItem.PlannedStoreId = visit.Store.Id;
                trip.Items.Add(tripItem);
            }

        db.ShoppingTrips.Add(trip);
        await db.SaveChangesAsync(ct);
        return trip;
    }

    /// <summary>
    /// Changes the stores on the trip. The home store always stays. Items not bought are spread again; items moved by
    /// hand stay put while their store is still on the trip.
    /// </summary>
    public async Task SetStoresAsync(ShoppingTrip trip, IReadOnlyCollection<int> storeIds, CancellationToken ct)
    {
        var context = await LoadContextAsync(trip.HouseholdId, ct);
        var chosen = context.Stores.Where(s => s.IsHome || storeIds.Contains(s.Id)).Select(s => s.Id).ToHashSet();

        trip.Stores.RemoveAll(s => !chosen.Contains(s.StoreId));
        foreach (var id in chosen.Where(id => trip.Stores.All(s => s.StoreId != id)))
            trip.Stores.Add(new ShoppingTripStore { StoreId = id });

        var open = trip.Items.Where(i => i.ShoppingListItem is { IsBought: false }).ToList();
        var visitable = StoreSelector.Visitable(context.Stores, chosen);
        var plan = StoreSelector.Distribute(context.Stores, chosen, [.. open.Select(i => TripPlanner.ToPlannerItem(
            i.ShoppingListItem!, context.Offers, i.MovedByHand && visitable.Contains(i.StoreId) ? i.StoreId : null))]);
        Place(plan, open, keepMoved: true);
        // The estimate covers what is still to buy on the new stores.
        trip.EstimatedSaving = plan.EstimatedSaving;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Moves one item to a store ("REMA was sold out"). The store joins the trip if it was not on it.</summary>
    public async Task<Result<bool>> MoveAsync(ShoppingTrip trip, int tripItemId, int storeId, CancellationToken ct)
    {
        var item = trip.Items.FirstOrDefault(i => i.Id == tripItemId);
        if (item is null) return Error.NotFound($"Item {tripItemId} is not on the trip");
        if (item.ShoppingListItem is not { } listItem) return Error.Invalid("The item is no longer on the shopping list");

        var context = await LoadContextAsync(trip.HouseholdId, ct);
        if (context.Stores.All(s => s.Id != storeId)) return Error.Invalid($"Store {storeId} is not one of the household's stores");

        var oldStoreId = item.StoreId;
        var offer = context.Offers.ForItem(listItem.IngredientId, listItem.Name, listItem.Variant, listItem.Quantity)
            .FirstOrDefault(o => o.StoreId == storeId); // cheapest first
        item.PlaceAt(storeId, offer?.Id, offer?.Heading, offer?.EffectivePrice, StoreSelector.PieceCount(listItem.Quantity),
            offer?.LastValidDay, byHand: true, offer?.AppLabel);
        listItem.PlannedStoreId = storeId;

        if (trip.Stores.All(s => s.StoreId != storeId)) trip.Stores.Add(new ShoppingTripStore { StoreId = storeId });
        // A store left without items leaves the trip (the home store always stays).
        if (oldStoreId != context.HomeId && trip.Items.All(i => i.StoreId != oldStoreId))
            trip.Stores.RemoveAll(s => s.StoreId == oldStoreId);

        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Takes list items added after the trip was made along, on the stores already on the trip.</summary>
    public async Task<int> AddNewItemsAsync(ShoppingTrip trip, CancellationToken ct)
    {
        var list = await ShoppingListViews.GetActiveListAsync(db, trip.HouseholdId, ct);
        var added = await TripViews.NewItems(db, list.Id, trip.Id).OrderBy(i => i.AddedAt).ToListAsync(ct);
        if (added.Count == 0) return 0;

        var context = await LoadContextAsync(trip.HouseholdId, ct);
        var plan = StoreSelector.Distribute(context.Stores, [.. trip.Stores.Select(s => s.StoreId)],
            [.. added.Select(i => TripPlanner.ToPlannerItem(i, context.Offers, null))]);
        var tripItems = added.Select(i => new ShoppingTripItem
        {
            ShoppingListItem = i, Name = i.Name, Quantity = i.Quantity, Variant = i.Variant, Tag = i.Tag,
        }).ToList();
        trip.Items.AddRange(tripItems);
        Place(plan, tripItems, keepMoved: false);
        trip.EstimatedSaving += plan.EstimatedSaving;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The other phone took the same items along a moment ago (unique index on trip + list item).
            // Its change notifies both phones, so nothing is lost by giving up here.
            trip.Items.RemoveAll(tripItems.Contains);
            db.ChangeTracker.Clear();
            return 0;
        }
        return added.Count;
    }

    private static void Place(StoreSelector.Plan plan, List<ShoppingTripItem> items, bool keepMoved)
    {
        var byListItem = items.ToDictionary(i => i.ShoppingListItem!.Id);
        foreach (var visit in plan.Visits)
            foreach (var planned in visit.Items)
            {
                var tripItem = byListItem[planned.Item.Id];
                var stillMoved = keepMoved && tripItem.MovedByHand && tripItem.StoreId == visit.Store.Id;
                TripPlanner.Apply(tripItem, planned, visit.Store.Id, stillMoved);
                tripItem.ShoppingListItem!.PlannedStoreId = visit.Store.Id;
            }
    }

    private async Task<Context> LoadContextAsync(int householdId, CancellationToken ct)
    {
        var stores = await ShoppingListViews.LoadStoresAsync(db, householdId, ct);
        var offers = await OfferLookup.LoadAsync(db, householdId, DanishTime.Today(clock), ct);
        return new Context([.. stores.Select(s => new StoreSelector.Store(s.Id, s.Name, s.IsHomeStore, s.StopGroup))], offers);
    }
}
