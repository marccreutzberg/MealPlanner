using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Meals;
using MealPlanner.Api.Domain;

namespace MealPlanner.Api.Features.MealPlans;

/// <summary>The meal plan for the week that contains a day. GET /api/meal-plans/{week} and get_meal_plan.</summary>
public static class GetMealPlan
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/meal-plans/{week}", async (DateOnly week, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(week, user.GetHouseholdId(), ct)));

    public sealed class Handler(AppDbContext db)
    {
        public Task<MealPlanViews.MealPlanDto> HandleAsync(DateOnly week, int householdId, CancellationToken ct) =>
            MealPlanViews.LoadAsync(db, householdId, MealPlan.MondayOf(week), ct);
    }
}
