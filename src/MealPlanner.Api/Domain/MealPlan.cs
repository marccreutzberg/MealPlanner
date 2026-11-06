namespace MealPlanner.Api.Domain;

/// <summary>One week, Monday to Sunday, identified by its Monday; one dish per day (spec 5.4).</summary>
public class MealPlan
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public DateOnly WeekStart { get; set; }
    public List<MealPlanEntry> Entries { get; set; } = [];

    public DateOnly WeekEnd => WeekStart.AddDays(6);

    public static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    public bool Contains(DateOnly day) => WeekStart <= day && day <= WeekEnd;

    /// <summary>Sets the dish for a day, or clears the day when recipeId is null. A new dish is not on the list yet.</summary>
    public void SetDay(DateOnly day, int? recipeId)
    {
        if (!Contains(day)) throw new ArgumentOutOfRangeException(nameof(day), $"{day} is not in the week of {WeekStart}");
        var existing = Entries.FirstOrDefault(e => e.Date == day);
        if (existing is not null && existing.RecipeId == recipeId) return;
        if (existing is not null) Entries.Remove(existing);
        if (recipeId is { } id) Entries.Add(new MealPlanEntry { Date = day, RecipeId = id });
    }

    /// <summary>Days whose ingredients are not on the shopping list yet (or all of them when forced).</summary>
    public List<MealPlanEntry> EntriesForList(bool includeAlreadyAdded) =>
        [.. Entries.Where(e => includeAlreadyAdded || e.AddedToListAt is null).OrderBy(e => e.Date)];
}
