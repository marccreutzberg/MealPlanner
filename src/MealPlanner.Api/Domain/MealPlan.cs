namespace MealPlanner.Api.Domain;

/// <summary>One week, Monday to Sunday, identified by its Monday; one dish per day (spec 5.4).</summary>
public class MealPlan
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public DateOnly WeekStart { get; set; }
    public List<MealPlanEntry> Entries { get; set; } = [];

    /// <summary>When the week's ingredients were put on the shopping list; guards against adding them twice.</summary>
    public DateTimeOffset? AddedToListAt { get; set; }

    public DateOnly WeekEnd => WeekStart.AddDays(6);

    public static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    public bool Contains(DateOnly day) => WeekStart <= day && day <= WeekEnd;

    /// <summary>Sets the dish for a day, or clears the day when recipeId is null.</summary>
    public void SetDay(DateOnly day, int? recipeId)
    {
        if (!Contains(day)) throw new ArgumentOutOfRangeException(nameof(day), $"{day} is not in the week of {WeekStart}");
        Entries.RemoveAll(e => e.Date == day);
        if (recipeId is { } id) Entries.Add(new MealPlanEntry { Date = day, RecipeId = id });
    }
}
