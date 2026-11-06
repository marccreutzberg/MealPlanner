using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Meals;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Recipes;

/// <summary>
/// Saves a recipe written by Claude (spec 5.1-5.2). Each ingredient line is matched to an ingredient with the same
/// matcher as offers; lines without a match are kept as text. Saving a name that exists replaces that recipe.
/// POST /api/recipes and add_recipe.
/// </summary>
public static class AddRecipe
{
    public sealed record Line(string Text, string? Quantity = null, bool IsMain = false);

    public sealed record Command(string Name, int Servings, int TotalMinutes, bool IsChildFriendly, List<Line> Ingredients,
        string? Method = null);

    public sealed record Response(RecipeViews.RecipeDto Recipe, List<string> Unmatched, bool Replaced);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/recipes", async (Command command, ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            (await handler.HandleAsync(command, user.GetHouseholdId(), ct)).ToHttpResult());

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Result<Response>> HandleAsync(Command c, int householdId, CancellationToken ct)
        {
            var name = c.Name?.Trim() ?? "";
            if (name.Length is 0 or > 200) return Error.Invalid("name is required (max 200 characters)");
            if (c.Servings is < 1 or > 50) return Error.Invalid("servings must be between 1 and 50");
            if (c.TotalMinutes is < 1 or > 1440) return Error.Invalid("totalMinutes must be between 1 and 1440");
            var lines = (c.Ingredients ?? []).Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();
            if (lines.Count == 0) return Error.Invalid("A recipe needs at least one ingredient");
            if (!lines.Any(l => l.IsMain))
                return Error.Invalid("Mark the main ingredients (isMain) - meal suggestions score recipes by them");
            if (lines.Any(l => l.Text.Trim().Length > 300)) return Error.Invalid("Ingredient text is max 300 characters");

            var matcher = await IngredientMatcher.LoadAsync(db, householdId, ct);
            var ingredients = lines.Select(l => new RecipeIngredient
            {
                Text = l.Text.Trim(),
                Quantity = string.IsNullOrWhiteSpace(l.Quantity) ? null : l.Quantity.Trim(),
                IsMain = l.IsMain,
                IngredientId = matcher.Match(l.Text).Cast<int?>().FirstOrDefault(),
            }).ToList();

            var recipe = await db.Recipes.Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.HouseholdId == householdId && r.Name == name, ct);
            var replaced = recipe is not null;
            if (recipe is null)
            {
                recipe = new Recipe { HouseholdId = householdId };
                db.Recipes.Add(recipe);
            }
            recipe.Update(name, c.Servings, c.TotalMinutes, c.IsChildFriendly, c.Method, ingredients);
            await db.SaveChangesAsync(ct);

            var ids = ingredients.Where(i => i.IngredientId != null).Select(i => i.IngredientId!.Value).ToList();
            var names = await db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
            return new Response(RecipeViews.ToDto(recipe, names),
                [.. ingredients.Where(i => i.IngredientId is null).Select(i => i.Text)], replaced);
        }
    }
}
