using MealPlanner.Api.Domain;

namespace MealPlanner.Tests.Domain;

public class MealPlanTests
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    [Theory]
    [InlineData("2026-11-09", "2026-11-09")] // Monday
    [InlineData("2026-11-12", "2026-11-09")] // Thursday
    [InlineData("2026-11-15", "2026-11-09")] // Sunday belongs to the week before the next Monday
    public void Monday_of(string day, string monday) =>
        Assert.Equal(DateOnly.Parse(monday), MealPlan.MondayOf(DateOnly.Parse(day)));

    [Fact]
    public void Set_replace_and_clear_a_day()
    {
        var plan = new MealPlan { WeekStart = Monday };
        plan.SetDay(Monday, 1);
        plan.SetDay(Monday.AddDays(1), 2);
        plan.SetDay(Monday, 3);
        plan.SetDay(Monday.AddDays(1), null);

        var entry = Assert.Single(plan.Entries);
        Assert.Equal((Monday, 3), (entry.Date, entry.RecipeId));
    }

    [Fact]
    public void Days_outside_the_week_are_refused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MealPlan { WeekStart = Monday }.SetDay(Monday.AddDays(7), 1));

    [Fact]
    public void Only_new_dishes_go_on_the_list_again()
    {
        var plan = new MealPlan { WeekStart = Monday };
        plan.SetDay(Monday, 1);
        plan.Entries[0].AddedToListAt = DateTimeOffset.UnixEpoch;
        plan.SetDay(Monday.AddDays(2), 2);
        plan.SetDay(Monday, 1); // same dish again keeps its "on the list" mark

        Assert.Equal([2], plan.EntriesForList(includeAlreadyAdded: false).Select(e => e.RecipeId));
        Assert.Equal(2, plan.EntriesForList(includeAlreadyAdded: true).Count);
    }
}
