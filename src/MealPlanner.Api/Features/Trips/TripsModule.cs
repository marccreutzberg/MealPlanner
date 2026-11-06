using MealPlanner.Api.Common.Trips;

namespace MealPlanner.Api.Features.Trips;

/// <summary>Saved shopping trips (design docs/superpowers/specs/2026-11-06-handleture-design.md).</summary>
public static class TripsModule
{
    public static IServiceCollection AddTrips(this IServiceCollection services)
    {
        services.AddScoped<TripService>();
        services.AddScoped<CreateTrip.Handler>();
        services.AddScoped<GetTrip.Handler>();
        services.AddScoped<ListTrips.Handler>();
        services.AddScoped<ChangeTrip.Handler>();
        services.AddScoped<AddTripItemsToList.Handler>();
        return services;
    }

    /// <summary>All trip endpoints accept the cookie or an API key, like the shopping list.</summary>
    public static IEndpointRouteBuilder MapTrips(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").RequireAuthorization();
        CreateTrip.Map(group);
        GetTrip.Map(group);
        ListTrips.Map(group);
        ChangeTrip.Map(group);
        AddTripItemsToList.Map(group);
        return app;
    }
}
