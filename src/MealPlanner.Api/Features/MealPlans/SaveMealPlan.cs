using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Meals;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans;

/// <summary>
/// Sets dishes for days in a week (spec 5.4). Only the given days change; a day without a recipe is cleared.
/// Recipes can be given by id or name. PUT /api/meal-plans/{week} and save_meal_plan.
/// </summary>
public static class SaveMealPlan
{
    public sealed record Day(DateOnly Date, int? RecipeId = null, string? Recipe = null);

    /// <param name="Week">Any day in the week; the plan is stored on its Monday.</param>
    public sealed record Command(DateOnly Week, List<Day> Days);

    public sealed record Body(List<Day> Days);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/meal-plans/{week}", async (DateOnly week, Body body, ClaimsPrincipal user, Handler handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(new Command(week, body.Days), user.GetHouseholdId(), ct)).ToHttpResult());

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Result<MealPlanViews.MealPlanDto>> HandleAsync(Command c, int householdId, CancellationToken ct)
        {
            var weekStart = MealPlan.MondayOf(c.Week);
            var days = c.Days ?? [];
            if (days.Count == 0) return Error.Invalid("Give at least one day");
            var outside = days.FirstOrDefault(d => d.Date < weekStart || d.Date > weekStart.AddDays(6));
            if (outside is not null) return Error.Invalid($"{outside.Date:yyyy-MM-dd} is not in the week of {weekStart:yyyy-MM-dd}");
            if (days.GroupBy(d => d.Date).Any(g => g.Count() > 1)) return Error.Invalid("One dish per day");

            var recipes = await db.Recipes.Where(r => r.HouseholdId == householdId)
                .Select(r => new { r.Id, r.Name }).ToListAsync(ct);

            var plan = await db.MealPlans.Include(p => p.Entries)
                .FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.WeekStart == weekStart, ct);
            if (plan is null)
            {
                plan = new MealPlan { HouseholdId = householdId, WeekStart = weekStart };
                db.MealPlans.Add(plan);
            }

            foreach (var day in days)
            {
                int? recipeId = null;
                if (day.RecipeId is { } id)
                {
                    if (recipes.All(r => r.Id != id)) return Error.NotFound($"Recipe {id} does not exist");
                    recipeId = id;
                }
                else if (!string.IsNullOrWhiteSpace(day.Recipe))
                {
                    var match = recipes.FirstOrDefault(r => string.Equals(r.Name, day.Recipe.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (match is null) return Error.NotFound($"No recipe named '{day.Recipe}'. Save it with add_recipe first.");
                    recipeId = match.Id;
                }
                plan.SetDay(day.Date, recipeId);
            }

            await db.SaveChangesAsync(ct);
            return await MealPlanViews.LoadAsync(db, householdId, weekStart, ct);
        }
    }
}
