using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Meals;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Recipes;

/// <summary>
/// Suggests dishes from the household's recipes ranked by the offers in the coming week (spec 5.3), each with the
/// store and offer price of its main ingredients. GET /api/recipes/suggestions and suggest_meals.
/// </summary>
public static class SuggestMeals
{
    private const int NotEatenForDays = 14;

    public sealed record Query(int Count = 5, bool? ChildFriendly = null, int? MaxMinutes = null, DateOnly? From = null);

    public sealed record MainDto(string Text, string? Ingredient, string? Store, decimal? Price, string? OfferHeading,
        DateOnly? LastValidDay);

    public sealed record Suggestion(int RecipeId, string Name, int TotalMinutes, bool IsChildFriendly, decimal Score,
        int MainsOnOffer, int Mains, decimal OfferTotal, List<MainDto> MainIngredients);

    public sealed record Response(DateOnly From, DateOnly To, int RecipesConsidered, List<Suggestion> Suggestions);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/recipes/suggestions", async (int? count, bool? childFriendly, int? maxMinutes, DateOnly? from,
                ClaimsPrincipal user, Handler handler, CancellationToken ct) =>
            (await handler.HandleAsync(new Query(count ?? 5, childFriendly, maxMinutes, from), user.GetHouseholdId(), ct))
            .ToHttpResult());

    public sealed class Handler(AppDbContext db, TimeProvider clock)
    {
        public async Task<Result<Response>> HandleAsync(Query q, int householdId, CancellationToken ct)
        {
            if (q.Count is < 1 or > 30) return Error.Invalid("count must be between 1 and 30");
            var today = DanishTime.Today(clock);
            var from = q.From ?? today;
            var to = from.AddDays(6);

            var recentlyEaten = await db.MealPlanEntries
                .Where(e => e.MealPlan.HouseholdId == householdId && e.Date >= today.AddDays(-NotEatenForDays) && e.Date < today)
                .Select(e => e.RecipeId)
                .Distinct()
                .ToListAsync(ct);

            var recipes = await db.Recipes.AsNoTracking().Include(r => r.Ingredients)
                .Where(r => r.HouseholdId == householdId && !recentlyEaten.Contains(r.Id))
                .Where(r => q.ChildFriendly != true || r.IsChildFriendly)
                .Where(r => q.MaxMinutes == null || r.TotalMinutes <= q.MaxMinutes)
                .ToListAsync(ct);

            var offers = await OfferLookup.LoadAsync(db, householdId, from, to, ct);
            var homeStoreId = await db.HouseholdStores.Where(h => h.HouseholdId == householdId && h.IsHomeStore)
                .Select(h => (int?)h.StoreId).FirstOrDefaultAsync(ct);
            var ingredientNames = await db.Ingredients.ToDictionaryAsync(i => i.Id, i => i.Name, ct);

            var ranked = MealScorer.Rank(recipes.Select(r => new MealScorer.Candidate(r.Id, r.Name, r.TotalMinutes,
                r.IsChildFriendly,
                [.. r.MainIngredients.Select(m => new MealScorer.Main(m.Text,
                    m.IngredientId is { } id ? ingredientNames.GetValueOrDefault(id) : null,
                    [.. offers.ForItem(m.IngredientId, m.Text, null).Select(o => new MealScorer.Offer(
                        o.Id, o.StoreName, o.Heading, o.Price, o.LastValidDay, o.StoreId == homeStoreId))]))])));

            return new Response(from, to, recipes.Count, [.. ranked.Take(q.Count).Select(s => new Suggestion(
                s.Recipe.RecipeId, s.Recipe.Name, s.Recipe.TotalMinutes, s.Recipe.IsChildFriendly, s.Score, s.OnOffer,
                s.Mains.Count, s.OfferTotal,
                [.. s.Mains.Select(m => new MainDto(m.Text, m.Ingredient, m.BestOffer?.Store, m.BestOffer?.Price,
                    m.BestOffer?.Heading, m.BestOffer?.LastValidDay))]))]);
        }
    }
}
