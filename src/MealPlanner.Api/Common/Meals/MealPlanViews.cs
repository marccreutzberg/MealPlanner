using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Meals;

/// <summary>A week's meal plan as the app and Claude see it: seven days, each with its dish or nothing.</summary>
public static class MealPlanViews
{
    public sealed record DayDto(DateOnly Date, RecipeViews.RecipeDto? Recipe, bool OnShoppingList);

    public sealed record MealPlanDto(DateOnly WeekStart, DateOnly WeekEnd, List<DayDto> Days);

    public static async Task<MealPlanDto> LoadAsync(AppDbContext db, int householdId, DateOnly weekStart, CancellationToken ct)
    {
        var plan = await db.MealPlans.AsNoTracking().AsSplitQuery()
            .Include(p => p.Entries).ThenInclude(e => e.Recipe).ThenInclude(r => r.Ingredients)
            .FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.WeekStart == weekStart, ct);

        var ingredientIds = plan?.Entries.SelectMany(e => e.Recipe.Ingredients)
            .Where(i => i.IngredientId != null).Select(i => i.IngredientId!.Value).Distinct().ToList() ?? [];
        var names = await db.Ingredients.Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);

        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = weekStart.AddDays(offset);
            var entry = plan?.Entries.FirstOrDefault(e => e.Date == date);
            return new DayDto(date, entry is null ? null : RecipeViews.ToDto(entry.Recipe, names), entry?.AddedToListAt is not null);
        }).ToList();
        return new MealPlanDto(weekStart, weekStart.AddDays(6), days);
    }
}
