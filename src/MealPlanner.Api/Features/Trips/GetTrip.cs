using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Common.Trips;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Localization;

namespace MealPlanner.Api.Features.Trips;

/// <summary>The active trip (GET /api/trips/active, 204 when there is none) or one trip by id (GET /api/trips/{id}). get_trip.</summary>
public static class GetTrip
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/trips/active", async Task<Results<Ok<TripViews.TripDto>, NoContent>> (ClaimsPrincipal user,
            Handler handler, CancellationToken ct) =>
            await handler.ActiveAsync(user.GetHouseholdId(), ct) is { } trip ? TypedResults.Ok(trip) : TypedResults.NoContent());

        app.MapGet("/api/trips/{id:int}", async (int id, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            (await handler.HandleAsync(id, user.GetHouseholdId(), ct)).ToHttpResult());
    }

    public sealed class Handler(TripService trips, AppDbContext db, IStringLocalizer<TripTexts> texts)
    {
        public async Task<TripViews.TripDto?> ActiveAsync(int householdId, CancellationToken ct) =>
            await trips.FindActiveAsync(householdId, ct) is { } trip ? await TripViews.LoadAsync(db, trip, texts, ct) : null;

        public async Task<Result<TripViews.TripDto>> HandleAsync(int tripId, int householdId, CancellationToken ct) =>
            await trips.FindAsync(householdId, tripId, ct) is { } trip
                ? await TripViews.LoadAsync(db, trip, texts, ct)
                : Error.NotFound($"Trip {tripId} not found");
    }
}
