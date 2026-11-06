using System.Globalization;
using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Search;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>
/// Type-ahead for the add box (spec 4, "Fuzzy søgning ved indtastning"): dictionary words, ingredients and what the
/// household has bought before. Exact beats prefix beats typo; ties go to what is bought most often.
/// GET /api/shopping-list/suggestions?q=.
/// </summary>
public static class SuggestItems
{
    public sealed record Suggestion(string Text, int TimesBought);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/shopping-list/suggestions", async (string? q, int? limit, ClaimsPrincipal user, AppDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return TypedResults.Ok(new List<Suggestion>());
            var householdId = user.GetHouseholdId();

            var bought = await db.PurchaseHistory
                .Where(p => p.HouseholdId == householdId)
                .GroupBy(p => p.ItemName)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var terms = await db.CategoryRules
                .Where(r => r.HouseholdId == null || r.HouseholdId == householdId)
                .Select(r => r.Term).ToListAsync(ct);
            var ingredients = await db.Ingredients
                .Where(i => i.HouseholdId == null || i.HouseholdId == householdId)
                .Select(i => i.Name).ToListAsync(ct);

            // Bought names keep their own spelling; dictionary words are lower case and get a capital letter.
            var vocabulary = new Dictionary<string, Suggestion>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in bought) vocabulary.TryAdd(b.Name, new Suggestion(b.Name, b.Count));
            foreach (var name in ingredients.Concat(terms.Select(Capitalize))) vocabulary.TryAdd(name, new Suggestion(name, 0));

            var result = vocabulary.Values
                .Select(s => (Suggestion: s, Score: FuzzySearch.Score(q, s.Text)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score).ThenByDescending(x => x.Suggestion.TimesBought).ThenBy(x => x.Suggestion.Text.Length)
                .Take(Math.Clamp(limit ?? 8, 1, 20))
                .Select(x => x.Suggestion)
                .ToList();
            return TypedResults.Ok(result);
        });

    private static string Capitalize(string term) =>
        term.Length == 0 ? term : char.ToUpper(term[0], CultureInfo.GetCultureInfo("da-DK")) + term[1..];
}
