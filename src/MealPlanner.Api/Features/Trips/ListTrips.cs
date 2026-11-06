using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Trips;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Trips;

/// <summary>Finished trips, newest first, one page at a time. GET /api/trips?skip=0&amp;take=20.</summary>
public static class ListTrips
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public sealed record Page(List<TripViews.TripSummaryDto> Items, int Total);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/trips", async (ClaimsPrincipal user, Handler handler, CancellationToken ct,
                int skip = 0, int take = DefaultPageSize) =>
            TypedResults.Ok(await handler.HandleAsync(user.GetHouseholdId(), skip, take, ct)));

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Page> HandleAsync(int householdId, int skip, int take, CancellationToken ct)
        {
            skip = Math.Max(skip, 0);
            take = Math.Clamp(take, 1, MaxPageSize);

            var finished = db.ShoppingTrips.Where(t => t.HouseholdId == householdId && t.FinishedAt != null);
            var total = await finished.CountAsync(ct);
            var trips = await finished
                .OrderByDescending(t => t.FinishedAt).ThenByDescending(t => t.Id)
                .Skip(skip).Take(take)
                .Include(t => t.Items)
                .AsSplitQuery()
                .ToListAsync(ct);

            var storeIds = trips.SelectMany(t => t.Items).Select(i => i.StoreId).Distinct().ToList();
            var names = await db.Stores.Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
            return new Page([.. trips.Select(t => TripViews.ToSummary(t, names))], total);
        }
    }
}
