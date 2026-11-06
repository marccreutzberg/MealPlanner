namespace MealPlanner.Api.Domain;

public class MealPlanEntry
{
    public int Id { get; set; }
    public int MealPlanId { get; set; }
    public MealPlan MealPlan { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    /// <summary>When this day's ingredients went on the shopping list; guards against adding them twice.</summary>
    public DateTimeOffset? AddedToListAt { get; set; }
}
