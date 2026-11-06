using System.Text;

namespace MealPlanner.Api.Common.Search;

/// <summary>
/// In-memory fuzzy scoring for short Danish product texts (spec 4, "Fuzzy søgning").
/// The vocabulary is small, so everything runs in .NET rather than SQL.
/// </summary>
public static class FuzzySearch
{
    // Score bands: exact > whole-text prefix > all words exact > word prefix > typo. 0 means no match.
    private const double Exact = 100, TextPrefix = 80, TextContains = 60;
    private const double WordExact = 3, WordPrefix = 2, WordContains = 1.5, WordTypo = 1;

    /// <summary>Scores how well the query matches the text; 0 when it does not match.</summary>
    public static double Score(string query, string text)
    {
        var q = Normalize(query);
        var t = Normalize(text);
        if (q.Length == 0 || t.Length == 0) return 0;

        var qJoined = Join(q);
        var tJoined = Join(t);
        if (tJoined == qJoined) return Exact;
        if (tJoined.StartsWith(qJoined, StringComparison.Ordinal)) return TextPrefix;

        var textWords = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (qJoined.Length >= 3 && tJoined.Contains(qJoined, StringComparison.Ordinal))
            return TextContains + (textWords.Any(w => w.StartsWith(qJoined, StringComparison.Ordinal)) ? 5 : 0);

        // Every query word must match some text word.
        var total = 0.0;
        var queryWords = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var queryWord in queryWords)
        {
            var best = textWords.Max(w => WordScore(queryWord, w));
            if (best == 0) return 0;
            total += best;
        }
        return total / queryWords.Length * (TextContains / WordExact) * 0.9;
    }

    private static double WordScore(string queryWord, string word)
    {
        if (word == queryWord) return WordExact;
        if (word.StartsWith(queryWord, StringComparison.Ordinal)) return WordPrefix;
        if (queryWord.Length >= 3 && word.Contains(queryWord, StringComparison.Ordinal)) return WordContains;

        var allowed = MaxTypos(queryWord);
        if (allowed == 0) return 0;
        // Compare with the whole word and with its start, so typos also work while still typing.
        var prefix = word.Length > queryWord.Length ? word[..queryWord.Length] : word;
        var distance = Math.Min(DamerauLevenshtein(queryWord, word), DamerauLevenshtein(queryWord, prefix));
        return distance <= allowed ? WordTypo : 0;
    }

    /// <summary>One typo in short words, two in long ones, none in very short ones.</summary>
    public static int MaxTypos(string word) => word.Length switch { < 3 => 0, <= 6 => 1, _ => 2 };

    /// <summary>
    /// Lower case; "ae", "oe", "aa" become "æ", "ø", "å"; hyphens become spaces; everything but letters and digits removed.
    /// </summary>
    public static string Normalize(string text)
    {
        var lower = text.ToLowerInvariant().Replace("ae", "æ").Replace("oe", "ø").Replace("aa", "å");
        var sb = new StringBuilder(lower.Length);
        var lastWasSpace = true;
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); lastWasSpace = false; }
            else if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; }
        }
        return sb.ToString().TrimEnd();
    }

    private static string Join(string normalized) => normalized.Replace(" ", "");

    /// <summary>Optimal string alignment distance: insert, delete, substitute, swap neighbours.</summary>
    public static int DamerauLevenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }
        return d[a.Length, b.Length];
    }
}
