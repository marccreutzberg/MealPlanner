using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.MealPlans;

/// <summary>
/// Puts the week's ingredients on the shopping list in one call (spec 5.5): the same ingredient across recipes becomes
/// one line with the amounts added up, and lines already on the list are merged. Days already added are skipped unless
/// forced. An ingredient used by one dish gets the dish as its trip group. POST /api/meal-plans/{week}/to-shopping-list.
/// </summary>
public static class AddMealPlanToList
{
    public sealed record Body(bool Force = false);

    public sealed record AddedLine(string Name, string? Quantity, List<string> Recipes);

    public sealed record Response(DateOnly WeekStart, List<string> Recipes, List<AddedLine> Items, int SkippedDays);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/meal-plans/{week}/to-shopping-list", async (DateOnly week, Body? body, ClaimsPrincipal user,
                Handler handler, CancellationToken ct) =>
            (await handler.HandleAsync(week, body?.Force ?? false, user.GetHouseholdId(), user.GetUserId(), ct)).ToHttpResult());

    public sealed class Handler(AppDbContext db, ShoppingListWriter writer, ListNotifier notifier, TimeProvider clock)
    {
        public async Task<Result<Response>> HandleAsync(DateOnly week, bool force, int householdId, string userId, CancellationToken ct)
        {
            var weekStart = MealPlan.MondayOf(week);
            var plan = await db.MealPlans.AsSplitQuery()
                .Include(p => p.Entries).ThenInclude(e => e.Recipe).ThenInclude(r => r.Ingredients)
                .FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.WeekStart == weekStart, ct);
            if (plan is null || plan.Entries.Count == 0) return Error.NotFound($"No meal plan for the week of {weekStart:yyyy-MM-dd}");

            var entries = plan.EntriesForList(force);
            if (entries.Count == 0)
                return Error.Invalid("This week's dishes are already on the shopping list. Use force to add them again.");

            var ingredientIds = entries.SelectMany(e => e.Recipe.Ingredients).Where(i => i.IngredientId != null)
                .Select(i => i.IngredientId!.Value).Distinct().ToList();
            var names = await db.Ingredients.Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);

            // One line per ingredient (or per text when unmatched), amounts summed across the week's dishes.
            var lines = entries
                .SelectMany(e => e.Recipe.Ingredients.Select(i => (Recipe: e.Recipe.Name, Ingredient: i)))
                .GroupBy(x => x.Ingredient.IngredientId is { } id ? "#" + id : IngredientMatcher.Normalize(x.Ingredient.Text))
                .Select(g =>
                {
                    var first = g.First().Ingredient;
                    var recipes = g.Select(x => x.Recipe).Distinct().ToList();
                    var name = first.IngredientId is { } id && names.TryGetValue(id, out var n) ? n : Capitalize(first.Text);
                    return new AddedLine(name, Quantity.Sum(g.Select(x => x.Ingredient.Quantity)), recipes);
                })
                .ToList();

            var written = await writer.AddAsync(
                [.. lines.Select(l => new ShoppingListWriter.NewItem(l.Name, l.Quantity, Tag: l.Recipes.Count == 1 ? l.Recipes[0] : null))],
                householdId, userId, ct);
            if (!written.IsSuccess) return written.Error!;

            var now = clock.GetUtcNow();
            foreach (var entry in entries) entry.AddedToListAt = now;
            await db.SaveChangesAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);

            return new Response(weekStart, [.. entries.Select(e => e.Recipe.Name).Distinct()], lines, plan.Entries.Count - entries.Count);
        }

        private static string Capitalize(string text)
        {
            var trimmed = text.Trim();
            return trimmed.Length == 0 ? trimmed : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        }
    }
}
