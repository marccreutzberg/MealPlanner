using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>
/// "Find tilbud": splits the list into a shopping trip by store (spec 4 and 12.6) and remembers each item's planned
/// store. POST /api/shopping-list/trip and build_shopping_list.
/// </summary>
public static class BuildTrip
{
    public sealed record TripItem(int ItemId, string Name, string? Variant, string? Quantity, string? Tag, bool IsBought,
        int? OfferId, string? OfferHeading, decimal? UnitPrice, int Count, decimal? LinePrice, DateOnly? LastValidDay);

    public sealed record Visit(int StoreId, string Store, string Color, bool IsHomeStore, decimal Subtotal, List<TripItem> Items);

    public sealed record Trip(List<Visit> Visits, decimal EstimatedSaving, string Text);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/shopping-list/trip", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(user.GetHouseholdId(), ct)));

    public sealed class Handler(AppDbContext db, TimeProvider clock, IStringLocalizer<TripTexts> texts)
    {
        public async Task<Trip> HandleAsync(int householdId, CancellationToken ct)
        {
            var list = await ShoppingListViews.GetActiveListAsync(db, householdId, ct);
            var items = await db.ShoppingListItems.Where(i => i.ShoppingListId == list.Id).OrderBy(i => i.AddedAt).ToListAsync(ct);
            var stores = await ShoppingListViews.LoadStoresAsync(db, householdId, ct);
            var threshold = await db.Households.Where(h => h.Id == householdId).Select(h => h.ThirdStoreThreshold).SingleAsync(ct);
            var offers = await OfferLookup.LoadAsync(db, householdId, DanishTime.Today(clock), ct);

            var plan = StoreSelector.Choose(
                [.. stores.Select(s => new StoreSelector.Store(s.Id, s.Name, s.IsHomeStore, s.StopGroup))],
                [.. items.Select(i => new StoreSelector.Item(i.Id, i.Name, i.Variant, i.Quantity, i.Tag,
                    // Items already bought stay where they were bought.
                    i.FlaggedStoreId ?? (i.IsBought ? i.PlannedStoreId : null), i.IsBought,
                    [.. offers.ForItem(i.IngredientId, i.Name, i.Variant).Select(o => new StoreSelector.Offer(
                        o.Id, o.StoreId, o.Heading, o.Price, o.PrePrice, o.IsEverydayPrice, o.LastValidDay))]))],
                threshold);

            var byId = items.ToDictionary(i => i.Id);
            foreach (var visit in plan.Visits)
                foreach (var planned in visit.Items.Where(p => !p.Item.IsBought))
                    byId[planned.Item.Id].PlannedStoreId = visit.Store.Id;
            await db.SaveChangesAsync(ct);

            var colors = stores.ToDictionary(s => s.Id, s => s.Color);
            return new Trip(
                [.. plan.Visits.Select(v => new Visit(v.Store.Id, v.Store.Name, colors[v.Store.Id], v.Store.IsHome, v.Subtotal,
                    [.. v.Items.Select(p => new TripItem(p.Item.Id, p.Item.Name, p.Item.Variant, p.Item.Quantity, p.Item.Tag,
                        p.Item.IsBought, p.Offer?.Id, p.Offer?.Heading, p.Offer?.Price, p.Count, p.LinePrice, p.Offer?.LastValidDay))]))],
                plan.EstimatedSaving,
                TripText.Format(plan, texts));
        }
    }
}
