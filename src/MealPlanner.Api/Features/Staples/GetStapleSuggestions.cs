using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Staples;

/// <summary>
/// "Plejer I at købe?" (spec 4): pinned items and items due by their buying rhythm, with an offer badge when on offer.
/// GET /api/staples/suggestions and suggest_staples.
/// </summary>
public static class GetStapleSuggestions
{
    private const int HistoryDays = 365;

    public sealed record SuggestionDto(string Name, string? Quantity, string Reason, int? RhythmDays, int? DaysSinceLast,
        DateOnly? LastBought, ShoppingListViews.OfferBadge? BestOffer);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/staples/suggestions", async (ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(user.GetHouseholdId(), ct)));

    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        public async Task<List<SuggestionDto>> HandleAsync(int householdId, CancellationToken ct)
        {
            var today = DanishTime.Today(clock);
            var since = clock.GetUtcNow().AddDays(-HistoryDays);

            var history = (await db.PurchaseHistory.AsNoTracking()
                    .Where(p => p.HouseholdId == householdId && p.BoughtAt >= since)
                    .Select(p => new { p.ItemName, p.Quantity, p.BoughtAt })
                    .ToListAsync(ct))
                .Select(p => new PurchaseRhythm.Purchase(p.ItemName, p.Quantity, DanishTime.ToDate(p.BoughtAt)));
            var pinned = await db.PinnedItems.AsNoTracking().Where(p => p.HouseholdId == householdId)
                .Select(p => new PurchaseRhythm.Pinned(p.ItemName, p.DefaultQuantity)).ToListAsync(ct);
            var onList = (await db.ShoppingListItems
                    .Where(i => i.ShoppingList.HouseholdId == householdId && i.ShoppingList.IsActive && !i.IsBought)
                    .Select(i => i.Name).ToListAsync(ct))
                .Select(PurchaseRhythm.Key).ToHashSet();
            var snoozed = (await db.ItemSnoozes.Where(s => s.HouseholdId == householdId && s.Until >= today)
                .Select(s => s.ItemName).ToListAsync(ct)).ToHashSet();

            var suggestions = PurchaseRhythm.Suggest(history, pinned, today, onList, snoozed);
            if (suggestions.Count == 0) return [];

            var offers = await OfferLookup.LoadAsync(db, householdId, today, ct);
            var matcher = await IngredientMatcher.LoadAsync(db, householdId, ct);
            return [.. suggestions.Select(s =>
            {
                var best = offers.ForItem(matcher.Match(s.Name).Cast<int?>().FirstOrDefault(), s.Name, null).FirstOrDefault();
                return new SuggestionDto(s.Name, s.Quantity, s.Reason.ToString(), s.RhythmDays, s.DaysSinceLast, s.LastBought,
                    best is null ? null : new ShoppingListViews.OfferBadge(best.Id, best.StoreName, best.Heading, best.Price,
                        best.LastValidDay, best.RequiresAppOrMembership));
            })];
        }
    }
}
