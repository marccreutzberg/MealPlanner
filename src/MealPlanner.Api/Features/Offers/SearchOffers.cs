using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Search;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Offers;

/// <summary>Free-text offer search with chain and period filters (spec 3.5). Used by GET /api/offers and search_offers.</summary>
public static class SearchOffers
{
    public enum Period
    {
        /// <summary>Valid today.</summary>
        Today,
        /// <summary>Valid on at least one of the next seven days, today included.</summary>
        Week,
        /// <summary>Starts after today (next catalogue).</summary>
        Upcoming,
    }

    public sealed record Query(string? Text, string? Store, Period Period = Period.Week, bool OnlyUnmapped = false, int Limit = 50);

    public sealed record OfferDto(
        int Id, string Store, string Heading, string? Description, decimal Price, decimal? PrePrice,
        DateOnly ValidFrom, DateOnly ValidTo, bool IsEverydayPrice, bool RequiresAppOrMembership, int? MaxPerCustomer,
        List<string> Ingredients);

    public sealed record Response(int Total, List<OfferDto> Offers);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/offers", async (string? text, string? store, string? period, bool? unmapped, int? limit,
                ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            {
                if (!TryParsePeriod(period, out var parsed))
                    return Results.Problem("period must be today, week or upcoming", statusCode: StatusCodes.Status400BadRequest);
                var query = new Query(text, store, parsed, unmapped ?? false, limit ?? 50);
                return (await handler.HandleAsync(query, user.GetHouseholdId(), ct)).ToHttpResult();
            })
            .RequireAuthorization();

    public static bool TryParsePeriod(string? value, out Period period)
    {
        period = Period.Week;
        return string.IsNullOrWhiteSpace(value) || Enum.TryParse(value, ignoreCase: true, out period);
    }

    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        private const int MaxLimit = 200;

        public async Task<Result<Response>> HandleAsync(Query query, int householdId, CancellationToken ct)
        {
            if (query.Limit is < 1 or > MaxLimit)
                return Error.Invalid($"limit must be between 1 and {MaxLimit}");

            var storeIds = db.HouseholdStores
                .Where(h => h.HouseholdId == householdId && h.IsActive)
                .Select(h => h.StoreId);

            var offers = db.Offers.AsNoTracking().Where(o => storeIds.Contains(o.StoreId));
            offers = FilterByPeriod(offers, query.Period, DanishTime.Today(clock));
            if (query.OnlyUnmapped)
                offers = offers.Where(o => !o.IsManuallyMapped && !o.Ingredients.Any());

            var candidates = await offers
                .Select(o => new
                {
                    Dto = new OfferDto(o.Id, o.Store.Name, o.Heading, o.Description, o.Price, o.PrePrice,
                        default, default, o.IsEverydayPrice, o.RequiresAppOrMembership, o.MaxPerCustomer,
                        o.Ingredients.Select(i => i.Ingredient.Name).ToList()),
                    o.ValidFrom,
                    o.ValidTo,
                })
                .ToListAsync(ct);

            // Chain and text filtering happen in memory: a few thousand rows, fuzzy matching is .NET-only.
            var matches = candidates
                .Where(c => string.IsNullOrWhiteSpace(query.Store)
                            || c.Dto.Store.Contains(query.Store.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(c => new
                {
                    Dto = c.Dto with
                    {
                        ValidFrom = DanishTime.ToDate(c.ValidFrom),
                        ValidTo = DanishTime.ToDate(c.ValidTo.AddSeconds(-1)),
                    },
                    Score = string.IsNullOrWhiteSpace(query.Text)
                        ? 1
                        : Math.Max(FuzzySearch.Score(query.Text, c.Dto.Heading),
                            c.Dto.Ingredients.Select(i => FuzzySearch.Score(query.Text, i)).DefaultIfEmpty(0).Max()),
                })
                .Where(m => m.Score > 0)
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.Dto.Price)
                .ToList();

            return new Response(matches.Count, [.. matches.Take(query.Limit).Select(m => m.Dto)]);
        }

        private static IQueryable<Offer> FilterByPeriod(IQueryable<Offer> offers, Period period, DateOnly today)
        {
            if (period == Period.Upcoming)
            {
                var tomorrow = DanishTime.StartOf(today.AddDays(1));
                return offers.Where(o => o.ValidFrom >= tomorrow);
            }

            var lastDay = period == Period.Today ? today : today.AddDays(6);
            var start = DanishTime.StartOf(today);
            var end = DanishTime.StartOf(lastDay.AddDays(1));
            return offers.Where(o => o.ValidFrom < end && o.ValidTo > start);
        }
    }
}
