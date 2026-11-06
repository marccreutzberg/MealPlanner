namespace MealPlanner.Api.Domain;

/// <summary>One week, Monday to Sunday, identified by its Monday.</summary>
public class MealPlan
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public DateOnly WeekStart { get; set; }
    public List<MealPlanEntry> Entries { get; set; } = [];
}
