using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Trips;

namespace MealPlanner.Api.Features.Trips;

/// <summary>
/// Puts chosen items from a trip (usually an old one) back on the list with name, amount, variant and group.
/// Items already on the list get the amounts added up. POST /api/trips/{id}/to-list.
/// </summary>
public static class AddTripItemsToList
{
    public sealed record Body(List<int> TripItemIds);

    public sealed record Added(int Count);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/trips/{id:int}/to-list", async (int id, Body body, ClaimsPrincipal user, Handler handler,
            CancellationToken ct) =>
            (await handler.HandleAsync(id, body.TripItemIds, user.GetHouseholdId(), user.GetUserId(), ct)).ToHttpResult());

    public sealed class Handler(TripService trips, ShoppingListWriter writer, ListNotifier notifier)
    {
        public async Task<Result<Added>> HandleAsync(int tripId, List<int> tripItemIds, int householdId, string userId,
            CancellationToken ct)
        {
            var trip = await trips.FindAsync(householdId, tripId, ct);
            if (trip is null) return Error.NotFound($"Trip {tripId} not found");

            var chosen = trip.Items
                .Where(i => tripItemIds.Contains(i.Id))
                .Select(i => TripViews.ToItem(i, active: false)!) // the frozen copy is what the archive shows
                .Select(i => new ShoppingListWriter.NewItem(i.Name, i.Quantity, i.Variant, i.Tag))
                .ToList();
            if (chosen.Count == 0) return Error.Invalid("Choose at least one item from the trip");

            var written = await writer.AddAsync(chosen, householdId, userId, ct);
            if (!written.IsSuccess) return written.Error!;
            await notifier.ListChangedAsync(householdId, ct);
            return new Added(chosen.Count);
        }
    }
}
