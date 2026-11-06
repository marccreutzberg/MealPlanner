using MealPlanner.Api.Common.Matching;

namespace MealPlanner.Api.Common.Lists;

/// <summary>
/// "Plejer I at købe?" (spec 4): from the purchase history, finds items that are due again, plus the household's
/// pinned items. Pure; no database.
/// </summary>
/// <remarks>
/// Rhythm = median of the days between purchases (robust against one odd trip); purchases on the same day count once.
/// It needs at least three purchase days. An item is due when 80 % of its rhythm has passed, and is left out when
/// it has not been bought for three rhythms (the household stopped buying it), when it is already on the list, or when
/// it was snoozed with "har nok".
/// </remarks>
public static class PurchaseRhythm
{
    public const decimal DueShare = 0.8m;
    private const int MinPurchaseDays = 3;
    private const int MaxIntervals = 6;
    private const int StoppedAfterRhythms = 3;

    public sealed record Purchase(string ItemName, string? Quantity, DateOnly Day);

    public sealed record Pinned(string ItemName, string? DefaultQuantity);

    public enum Reason { Pinned, Rhythm }

    public sealed record Suggestion(string Name, string? Quantity, Reason Reason, int? RhythmDays, int? DaysSinceLast,
        DateOnly? LastBought);

    public static List<Suggestion> Suggest(IEnumerable<Purchase> history, IEnumerable<Pinned> pinned, DateOnly today,
        ISet<string> onList, ISet<string> snoozed)
    {
        bool Skip(string name) => onList.Contains(Key(name)) || snoozed.Contains(Key(name));

        var byItem = history.GroupBy(p => Key(p.ItemName)).ToDictionary(g => g.Key, g => g.OrderBy(p => p.Day).ToList());
        var suggestions = new List<(Suggestion Suggestion, decimal Urgency)>();

        foreach (var pin in pinned.Where(p => !Skip(p.ItemName)))
        {
            var last = byItem.GetValueOrDefault(Key(pin.ItemName))?.LastOrDefault();
            suggestions.Add((new Suggestion(pin.ItemName, pin.DefaultQuantity ?? last?.Quantity, Reason.Pinned,
                Rhythm(byItem.GetValueOrDefault(Key(pin.ItemName))), last is null ? null : today.DayNumber - last.Day.DayNumber,
                last?.Day), decimal.MaxValue));
        }

        var pinnedKeys = pinned.Select(p => Key(p.ItemName)).ToHashSet();
        foreach (var (key, purchases) in byItem)
        {
            if (pinnedKeys.Contains(key) || Skip(key)) continue;
            if (Rhythm(purchases) is not { } rhythm) continue;

            var last = purchases[^1];
            var since = today.DayNumber - last.Day.DayNumber;
            if (since < DueShare * rhythm || since > StoppedAfterRhythms * rhythm) continue;

            // The spelling and amount from the latest purchase.
            var quantity = purchases.LastOrDefault(p => !string.IsNullOrWhiteSpace(p.Quantity))?.Quantity;
            suggestions.Add((new Suggestion(last.ItemName, quantity, Reason.Rhythm, rhythm, since, last.Day), (decimal)since / rhythm));
        }

        return [.. suggestions.OrderByDescending(s => s.Urgency).ThenBy(s => s.Suggestion.Name).Select(s => s.Suggestion)];
    }

    /// <summary>Median days between the latest purchase days, or null with fewer than three purchase days.</summary>
    public static int? Rhythm(IReadOnlyList<Purchase>? purchases)
    {
        if (purchases is null) return null;
        var days = purchases.Select(p => p.Day).Distinct().Order().ToList();
        if (days.Count < MinPurchaseDays) return null;

        var gaps = days.Zip(days.Skip(1), (a, b) => b.DayNumber - a.DayNumber).TakeLast(MaxIntervals).Order().ToList();
        var middle = gaps.Count / 2;
        var median = gaps.Count % 2 == 1 ? gaps[middle] : (gaps[middle - 1] + gaps[middle]) / 2.0;
        return Math.Max(1, (int)Math.Round(median, MidpointRounding.AwayFromZero));
    }

    /// <summary>How long "har nok" hides a suggestion: half the rhythm, at least two days.</summary>
    public static int SnoozeDays(int? rhythmDays) => Math.Max(2, (rhythmDays ?? 4) / 2);

    /// <summary>Item names are compared normalised ("Mælk", "mælk " and "MÆLK" are the same item).</summary>
    public static string Key(string name) => IngredientMatcher.Normalize(name);
}
