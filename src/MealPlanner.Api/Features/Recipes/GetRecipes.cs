using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Meals;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Recipes;

/// <summary>The household's recipes. GET /api/recipes and GET /api/recipes/{id}.</summary>
public static class GetRecipes
{
    public sealed record Summary(int Id, string Name, int TotalMinutes, bool IsChildFriendly, List<string> MainIngredients);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/recipes", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var householdId = user.GetHouseholdId();
            var recipes = await db.Recipes.AsNoTracking()
                .Where(r => r.HouseholdId == householdId)
                .OrderBy(r => r.Name)
                .Select(r => new Summary(r.Id, r.Name, r.TotalMinutes, r.IsChildFriendly,
                    r.Ingredients.Where(i => i.IsMain).Select(i => i.Text).ToList()))
                .ToListAsync(ct);
            return TypedResults.Ok(recipes);
        });

        app.MapGet("/api/recipes/{id:int}", async Task<Results<Ok<RecipeViews.RecipeDto>, NotFound>> (int id,
            ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var householdId = user.GetHouseholdId();
            var recipe = await db.Recipes.AsNoTracking().Include(r => r.Ingredients)
                .SingleOrDefaultAsync(r => r.Id == id && r.HouseholdId == householdId, ct);
            if (recipe is null) return TypedResults.NotFound();

            var ids = recipe.Ingredients.Where(i => i.IngredientId != null).Select(i => i.IngredientId!.Value).ToList();
            var names = await db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
            return TypedResults.Ok(RecipeViews.ToDto(recipe, names));
        });
    }
}
