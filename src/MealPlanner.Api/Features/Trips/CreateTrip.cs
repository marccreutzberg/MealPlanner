using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Common.Trips;
using Microsoft.Extensions.Localization;

namespace MealPlanner.Api.Features.Trips;

/// <summary>
/// "Opret handletur": everything not bought yet, spread over the stores the planner picks or the ones given.
/// Finishes the active trip first. POST /api/trips and build_shopping_list.
/// </summary>
public static class CreateTrip
{
    public sealed record Command(List<int>? StoreIds = null);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/trips", async (Command? body, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            (await handler.HandleAsync(body ?? new Command(), user.GetHouseholdId(), user.GetUserId(), ct)).ToHttpResult());

    public sealed class Handler(TripService trips, AppDbContext db, ListNotifier notifier, IStringLocalizer<TripTexts> texts)
    {
        public async Task<Result<TripViews.TripDto>> HandleAsync(Command command, int householdId, string? userId,
            CancellationToken ct)
        {
            var trip = await trips.CreateAsync(householdId, userId, command.StoreIds, ct);
            await notifier.ListChangedAsync(householdId, ct);
            return await TripViews.LoadAsync(db, trip, texts, ct);
        }
    }
}
