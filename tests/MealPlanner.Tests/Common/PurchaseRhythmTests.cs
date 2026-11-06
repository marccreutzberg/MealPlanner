using MealPlanner.Api.Common.Lists;
using static MealPlanner.Api.Common.Lists.PurchaseRhythm;

namespace MealPlanner.Tests.Common;

public class PurchaseRhythmTests
{
    private static readonly DateOnly Today = new(2026, 11, 6);

    /// <summary>Purchases on the given days before today.</summary>
    private static IEnumerable<Purchase> Bought(string name, string? quantity, params int[] daysAgo) =>
        daysAgo.Select(d => new Purchase(name, quantity, Today.AddDays(-d)));

    private static List<Suggestion> Run(IEnumerable<Purchase> history, IEnumerable<Pinned>? pinned = null,
        string[]? onList = null, string[]? snoozed = null) =>
        Suggest(history, pinned ?? [], Today, (onList ?? []).Select(Key).ToHashSet(), (snoozed ?? []).Select(Key).ToHashSet());

    [Fact]
    public void Milk_every_week_is_due_after_six_days()
    {
        // Bought 6, 13, 20 and 27 days ago: rhythm 7, 6 days since > 0.8 x 7.
        var suggestion = Assert.Single(Run(Bought("Mælk", "2 l", 6, 13, 20, 27)));
        Assert.Equal(("Mælk", "2 l", Reason.Rhythm, 7, 6), (suggestion.Name, suggestion.Quantity, suggestion.Reason,
            suggestion.RhythmDays, suggestion.DaysSinceLast));
    }

    [Fact]
    public void Not_due_before_eighty_percent() => Assert.Empty(Run(Bought("Mælk", null, 5, 12, 19)));

    [Fact]
    public void Needs_three_purchase_days() => Assert.Empty(Run(Bought("Kaffe", null, 30, 60)));

    [Fact]
    public void Same_day_purchases_count_once() => Assert.Null(Rhythm([.. Bought("Æg", null, 10, 10, 20)]));

    [Fact]
    public void Median_ignores_one_odd_trip()
    {
        // Gaps 7, 7, 1, 7 -> median 7, not the average 5,5.
        Assert.Equal(7, Rhythm([.. Bought("Brød", null, 0, 7, 14, 15, 22)]));
    }

    [Fact]
    public void Stopped_buying_after_three_rhythms() => Assert.Empty(Run(Bought("Bleer", null, 40, 47, 54)));

    [Fact]
    public void Items_on_the_list_or_snoozed_are_left_out()
    {
        var history = Bought("Mælk", null, 7, 14, 21).Concat(Bought("Smør", null, 7, 14, 21)).ToList();
        Assert.Empty(Run(history, onList: ["mælk"], snoozed: ["SMØR"]));
    }

    [Fact]
    public void Pinned_items_come_first_with_their_default_amount()
    {
        var result = Run(Bought("Mælk", "1 l", 7, 14, 21), pinned: [new Pinned("Bananer", "6 stk.")]);
        Assert.Equal(["Bananer", "Mælk"], result.Select(s => s.Name));
        Assert.Equal((Reason.Pinned, "6 stk."), (result[0].Reason, result[0].Quantity));
    }

    [Fact]
    public void Pinned_item_on_the_list_is_not_suggested() =>
        Assert.Empty(Run([], pinned: [new Pinned("Bananer", null)], onList: ["Bananer"]));

    [Fact]
    public void Most_overdue_first()
    {
        var history = Bought("Mælk", null, 7, 14, 21).Concat(Bought("Kaffe", null, 20, 34, 48)).ToList();
        // Mælk 7/7 = 1.0, Kaffe 20/14 = 1.43
        Assert.Equal(["Kaffe", "Mælk"], Run(history).Select(s => s.Name));
    }

    [Theory]
    [InlineData(7, 3)]
    [InlineData(2, 2)]
    [InlineData(null, 2)]
    [InlineData(30, 15)]
    public void Snooze_is_half_the_rhythm(int? rhythm, int expected) => Assert.Equal(expected, SnoozeDays(rhythm));
}
