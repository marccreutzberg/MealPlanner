namespace MealPlanner.Api.Domain;

/// <summary>
/// Maps a word to a category. Shared when HouseholdId is null, otherwise learned by one household.
/// </summary>
public class CategoryRule
{
    public int Id { get; set; }
    public string Term { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public bool IsLearned { get; set; }
    public int? HouseholdId { get; set; }
    public Household? Household { get; set; }
}
