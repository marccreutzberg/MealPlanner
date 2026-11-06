using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Common.Trips;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace MealPlanner.Api.Features.Trips;

/// <summary>
/// Changes to the active trip: choose stores, move one item, take new list items along, finish.
/// Each answers with the trip as it is now; a finished trip cannot be changed. Also set_trip_stores,
/// move_trip_item, add_new_items_to_trip and finish_trip.
/// </summary>
public static class ChangeTrip
{
    public sealed record StoresBody(List<int> StoreIds);

    public sealed record MoveBody(int StoreId);

    /// <param name="Remaining">Items left on the list, not bought; they come along on the next trip.</param>
    public sealed record Finished(int Remaining);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/trips/{id:int}/stores", async (int id, StoresBody body, ClaimsPrincipal user, Handler handler,
            CancellationToken ct) => (await handler.SetStoresAsync(id, body.StoreIds, user.GetHouseholdId(), ct)).ToHttpResult());

        app.MapPost("/api/trips/{id:int}/items/{tripItemId:int}/store", async (int id, int tripItemId, MoveBody body,
            ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            (await handler.MoveAsync(id, tripItemId, body.StoreId, user.GetHouseholdId(), ct)).ToHttpResult());

        app.MapPost("/api/trips/{id:int}/new-items", async (int id, ClaimsPrincipal user, Handler handler,
            CancellationToken ct) => (await handler.AddNewItemsAsync(id, user.GetHouseholdId(), ct)).ToHttpResult());

        app.MapPost("/api/trips/{id:int}/finish", async (int id, ClaimsPrincipal user, Handler handler,
            CancellationToken ct) => (await handler.FinishAsync(id, user.GetHouseholdId(), ct)).ToHttpResult());
    }

    public sealed class Handler(TripService trips, AppDbContext db, ListNotifier notifier, TimeProvider clock,
        IStringLocalizer<TripTexts> texts)
    {
        public Task<Result<TripViews.TripDto>> SetStoresAsync(int tripId, List<int> storeIds, int householdId,
            CancellationToken ct) =>
            ChangeAsync(tripId, householdId, trip => trips.SetStoresAsync(trip, storeIds, ct), ct);

        public async Task<Result<TripViews.TripDto>> MoveAsync(int tripId, int tripItemId, int storeId, int householdId,
            CancellationToken ct)
        {
            var found = await trips.FindEditableAsync(householdId, tripId, ct);
            if (!found.IsSuccess) return found.Error!;

            var moved = await trips.MoveAsync(found.Value!, tripItemId, storeId, ct);
            if (!moved.IsSuccess) return moved.Error!;

            await notifier.ListChangedAsync(householdId, ct);
            return await TripViews.LoadAsync(db, found.Value!, texts, ct);
        }

        public Task<Result<TripViews.TripDto>> AddNewItemsAsync(int tripId, int householdId, CancellationToken ct) =>
            ChangeAsync(tripId, householdId, trip => trips.AddNewItemsAsync(trip, ct), ct);

        public async Task<Result<Finished>> FinishAsync(int tripId, int householdId, CancellationToken ct)
        {
            var found = await trips.FindEditableAsync(householdId, tripId, ct);
            if (!found.IsSuccess) return found.Error!;

            found.Value!.Finish(clock.GetUtcNow());
            await db.SaveChangesAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);

            var list = await ShoppingListViews.GetActiveListAsync(db, householdId, ct);
            return new Finished(await db.ShoppingListItems.CountAsync(i => i.ShoppingListId == list.Id && !i.IsBought, ct));
        }

        private async Task<Result<TripViews.TripDto>> ChangeAsync(int tripId, int householdId,
            Func<ShoppingTrip, Task> change, CancellationToken ct)
        {
            var found = await trips.FindEditableAsync(householdId, tripId, ct);
            if (!found.IsSuccess) return found.Error!;

            await change(found.Value!);
            await notifier.ListChangedAsync(householdId, ct);
            return await TripViews.LoadAsync(db, found.Value!, texts, ct);
        }
    }
}
