using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Categories;

/// <summary>
/// Puts a shopping item in a category (spec 4, "Automatisk kategori"). Pure and in-memory; load one per request.
/// </summary>
/// <remarks>
/// Order: the household's learned rules for the exact name, a shared rule for the exact name, an ingredient match,
/// then the longest dictionary word found in the name, whole or as the end of a compound word
/// ("rugbrød" -> "brød", "rygeost" -> "ost"). Nothing found: <see cref="FallbackCategory"/>.
/// </remarks>
public sealed class CategoryClassifier
{
    public const string FallbackCategory = "Andet";
    private const int MinCompoundTail = 3;

    public sealed record Rule(string Term, int CategoryId, bool IsHousehold);

    private readonly Dictionary<string, int> householdExact;
    private readonly Dictionary<string, int> sharedExact;
    private readonly List<(string Term, int CategoryId)> sharedByLength;
    private readonly IngredientMatcher matcher;
    private readonly Dictionary<int, int> ingredientCategories;
    private readonly int fallbackCategoryId;

    public CategoryClassifier(IEnumerable<Rule> rules, IngredientMatcher matcher,
        Dictionary<int, int> ingredientCategories, int fallbackCategoryId)
    {
        var list = rules.Select(r => r with { Term = IngredientMatcher.Normalize(r.Term) }).Where(r => r.Term.Length > 0).ToList();
        householdExact = list.Where(r => r.IsHousehold).GroupBy(r => r.Term).ToDictionary(g => g.Key, g => g.Last().CategoryId);
        sharedExact = list.Where(r => !r.IsHousehold).GroupBy(r => r.Term).ToDictionary(g => g.Key, g => g.First().CategoryId);
        sharedByLength = [.. sharedExact.Select(kv => (kv.Key, kv.Value)).OrderByDescending(r => r.Key.Length)];
        this.matcher = matcher;
        this.ingredientCategories = ingredientCategories;
        this.fallbackCategoryId = fallbackCategoryId;
    }

    public static async Task<CategoryClassifier> LoadAsync(AppDbContext db, int householdId, CancellationToken ct)
    {
        var rules = await db.CategoryRules
            .Where(r => r.HouseholdId == null || r.HouseholdId == householdId)
            .Select(r => new Rule(r.Term, r.CategoryId, r.HouseholdId != null))
            .ToListAsync(ct);
        var ingredientCategories = await db.Ingredients
            .Where(i => i.HouseholdId == null || i.HouseholdId == householdId)
            .ToDictionaryAsync(i => i.Id, i => i.CategoryId, ct);
        var fallback = await db.Categories.Where(c => c.Name == FallbackCategory).Select(c => c.Id).FirstAsync(ct);
        return new CategoryClassifier(rules, await IngredientMatcher.LoadAsync(db, householdId, ct), ingredientCategories, fallback);
    }

    public sealed record Classification(int CategoryId, int? IngredientId);

    public Classification Classify(string itemName)
    {
        var name = IngredientMatcher.Normalize(itemName);
        var ingredientId = matcher.Match(name).Cast<int?>().FirstOrDefault();

        if (householdExact.TryGetValue(name, out var learned)) return new(learned, ingredientId);
        if (sharedExact.TryGetValue(name, out var exact)) return new(exact, ingredientId);
        if (ingredientId is { } id && ingredientCategories.TryGetValue(id, out var fromIngredient)) return new(fromIngredient, id);

        var words = name.Split([' ', '-', ',', '/'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var (term, categoryId) in sharedByLength)
        {
            if (term.Contains(' ') ? name.Contains(term, StringComparison.Ordinal) : words.Any(w => IsWordOrTail(w, term)))
                return new(categoryId, ingredientId);
        }
        return new(fallbackCategoryId, ingredientId);
    }

    private static bool IsWordOrTail(string word, string term) =>
        word == term || (term.Length >= MinCompoundTail && word.EndsWith(term, StringComparison.Ordinal));
}
