using System.ComponentModel;
using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Trips;
using MealPlanner.Api.Features.Trips;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MealPlanner.Api.Mcp;

/// <summary>MCP tools for the saved shopping trip. Same handlers as the /api/trips endpoints. Create it with build_shopping_list.</summary>
[McpServerToolType]
public sealed class TripTools(IHttpContextAccessor http)
{
    private ClaimsPrincipal User => (http.HttpContext ?? throw new McpException("No request context")).User;

    [McpServerTool(Name = "get_trip", Title = "Get the shopping trip", ReadOnly = true, Idempotent = true)]
    [Description("The shopping trip in progress (or a finished one by id): items per store in driving order with " +
                 "ticks, known prices and subtotals, the stores the household can choose from, items added to the list " +
                 "after the trip was made (newItems), and the trip as plain text. Texts are Danish.")]
    public async Task<TripViews.TripDto> GetTripAsync(
        GetTrip.Handler handler,
        [Description("A finished trip's id; leave out for the trip in progress.")] int? tripId = null,
        CancellationToken cancellationToken = default)
    {
        if (tripId is { } id) return Unwrap(await handler.HandleAsync(id, User.GetHouseholdId(), cancellationToken));
        return await handler.ActiveAsync(User.GetHouseholdId(), cancellationToken)
            ?? throw new McpException("No shopping trip in progress. Create one with build_shopping_list.");
    }

    [McpServerTool(Name = "set_trip_stores", Title = "Choose the trip's stores", Destructive = false)]
    [Description("Sets which stores are on the trip in progress; REMA 1000 always stays, and Lidl and Coop 365 count as " +
                 "one stop. Items not bought yet are spread again over these stores (items moved by hand stay put while " +
                 "their store is on the trip).")]
    public async Task<TripViews.TripDto> SetTripStoresAsync(
        ChangeTrip.Handler handler, TripService trips, AppDbContext db,
        [Description("Store names, e.g. ['Lidl', 'Netto'].")] List<string> stores,
        CancellationToken cancellationToken = default)
    {
        var trip = await ActiveTripIdAsync(trips, cancellationToken);
        var storeIds = Unwrap(await StoreNames.ResolveAsync(db, User.GetHouseholdId(), stores, cancellationToken));
        return Unwrap(await handler.SetStoresAsync(trip, storeIds, User.GetHouseholdId(), cancellationToken));
    }

    [McpServerTool(Name = "move_trip_item", Title = "Move an item to another store", Destructive = false)]
    [Description("Moves one item on the trip in progress to another store, e.g. because REMA 1000 is sold out. The " +
                 "store joins the trip if it was not on it. Only this trip; the item's usual store is not changed.")]
    public async Task<TripViews.TripDto> MoveTripItemAsync(
        ChangeTrip.Handler handler, TripService trips, AppDbContext db,
        [Description("The item's id on the trip (visits[].items[].id from get_trip).")] int tripItemId,
        [Description("Store name, e.g. 'Kvickly'.")] string store,
        CancellationToken cancellationToken = default)
    {
        var trip = await ActiveTripIdAsync(trips, cancellationToken);
        var storeId = Unwrap(await StoreNames.ResolveAsync(db, User.GetHouseholdId(), [store], cancellationToken)).Single();
        return Unwrap(await handler.MoveAsync(trip, tripItemId, storeId, User.GetHouseholdId(), cancellationToken));
    }

    [McpServerTool(Name = "add_new_items_to_trip", Title = "Take new list items along", Destructive = false)]
    [Description("Puts the items added to the list after the trip was made (newItems in get_trip) on the trip in " +
                 "progress, spread over the stores already on it.")]
    public async Task<TripViews.TripDto> AddNewItemsToTripAsync(
        ChangeTrip.Handler handler, TripService trips, CancellationToken cancellationToken = default) =>
        Unwrap(await handler.AddNewItemsAsync(await ActiveTripIdAsync(trips, cancellationToken), User.GetHouseholdId(),
            cancellationToken));

    [McpServerTool(Name = "finish_trip", Title = "Finish the shopping trip", Destructive = false)]
    [Description("Finishes the trip in progress and keeps it as history. Items not bought stay on the list for the " +
                 "next trip; returns how many.")]
    public async Task<ChangeTrip.Finished> FinishTripAsync(
        ChangeTrip.Handler handler, TripService trips, CancellationToken cancellationToken = default) =>
        Unwrap(await handler.FinishAsync(await ActiveTripIdAsync(trips, cancellationToken), User.GetHouseholdId(),
            cancellationToken));

    private async Task<int> ActiveTripIdAsync(TripService trips, CancellationToken ct) =>
        (await trips.FindActiveAsync(User.GetHouseholdId(), ct))?.Id
        ?? throw new McpException("No shopping trip in progress. Create one with build_shopping_list.");

    private static T Unwrap<T>(Result<T> result) =>
        result.IsSuccess ? result.Value! : throw new McpException(result.Error!.Message);
}
