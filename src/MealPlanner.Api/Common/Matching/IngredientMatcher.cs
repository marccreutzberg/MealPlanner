using System.Text;
using System.Text.RegularExpressions;
using MealPlanner.Api.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Matching;

/// <summary>
/// Matches free text (offer headings, later recipe lines and shopping items) to ingredients
/// using each ingredient's keywords and exclusion words. Pure and in-memory; build one per batch.
/// </summary>
/// <remarks>
/// Rules (see spec 12.4):
/// - The text is split on ",", "eller" and "el." because one heading often lists several products.
/// - Keywords of four letters or less must be a whole word ("ris" must not hit "pris", "æg" not "pålæg").
///   Longer keywords may sit inside compound words ("kartofler" in "bagekartofler").
/// - Exclusion words are checked in the same part of the text as the keyword.
/// - "-lårfilet" after "kyllingebrystfilet" means "kyllingelårfilet": a dash word matches a keyword that
///   ends with it when the rest of the keyword ("kyllinge") appears elsewhere in the text.
/// </remarks>
public sealed partial class IngredientMatcher
{
    private const int WholeWordMaxLength = 4;

    public sealed record Candidate(int IngredientId, string Name, List<string> Keywords, List<string> Exclusions);

    private readonly List<Candidate> candidates;

    public IngredientMatcher(IEnumerable<Candidate> candidates)
    {
        this.candidates = [.. candidates.Select(c => c with
        {
            Keywords = [.. c.Keywords.Select(Normalize).Where(k => k.Length > 0).Distinct()],
            Exclusions = [.. c.Exclusions.Select(Normalize).Where(x => x.Length > 0).Distinct()],
        })];
    }

    /// <summary>Shared keywords plus, when given, the household's own.</summary>
    public static async Task<IngredientMatcher> LoadAsync(AppDbContext db, int? householdId, CancellationToken ct)
    {
        var aliases = await db.IngredientAliases
            .Where(a => a.HouseholdId == null || a.HouseholdId == householdId)
            .Select(a => new { a.IngredientId, a.Ingredient.Name, a.Text, a.IsExclusion })
            .ToListAsync(ct);

        return new IngredientMatcher(aliases
            .GroupBy(a => (a.IngredientId, a.Name))
            .Select(g => new Candidate(g.Key.IngredientId, g.Key.Name,
                [.. g.Where(a => !a.IsExclusion).Select(a => a.Text)],
                [.. g.Where(a => a.IsExclusion).Select(a => a.Text)])));
    }

    /// <returns>Ids of all ingredients the text matches, in no particular order.</returns>
    public List<int> Match(string text)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0) return [];

        var parts = PartSeparator().Split(normalized).Where(p => p.Length > 0).ToList();
        var matches = new List<int>();
        foreach (var candidate in candidates)
        {
            if (parts.Any(part => MatchesPart(candidate, part, normalized)))
                matches.Add(candidate.IngredientId);
        }
        return matches;
    }

    private static bool MatchesPart(Candidate candidate, string part, string wholeText)
    {
        if (candidate.Exclusions.Any(part.Contains)) return false;

        var words = Words(part);
        foreach (var keyword in candidate.Keywords)
        {
            if (IsShortSingleWord(keyword) ? words.Contains(keyword) : part.Contains(keyword))
                return true;

            foreach (var dashWord in words.Where(w => w.Length > 2 && w[0] == '-'))
            {
                var tail = dashWord[1..];
                if (keyword.Length > tail.Length + 2 && keyword.EndsWith(tail, StringComparison.Ordinal)
                    && wholeText.Contains(keyword[..^tail.Length]))
                    return true;
            }
        }
        return false;
    }

    private static bool IsShortSingleWord(string keyword) => keyword.Length <= WholeWordMaxLength && !keyword.Contains(' ');

    private static HashSet<string> Words(string part)
    {
        var words = new HashSet<string>(part.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        // "Herregårds-rødkål" also counts as the words "herregårds" and "rødkål".
        foreach (var word in words.ToList().Where(w => w.IndexOf('-') > 0))
            words.UnionWith(word.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return words;
    }

    /// <summary>
    /// Lower case, emojis and symbols removed, whitespace collapsed.
    /// Keeps letters, digits and "&amp; - . , /" (the last two separate products in a heading).
    /// </summary>
    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) || ch is '&' or '-' or '.' or ',' or '/' ? ch : ' ');
        return Whitespace().Replace(sb.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\s*,\s*|\s+eller\s+|\s+el\.\s+|\s*/\s*")]
    private static partial Regex PartSeparator();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
