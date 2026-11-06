namespace MealPlanner.Api.Domain;

/// <summary>
/// A search keyword for an ingredient, or an exclusion word that blocks a match.
/// Shared when HouseholdId is null.
/// </summary>
public class IngredientAlias
{
    public int Id { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public string Text { get; set; } = "";
    public bool IsExclusion { get; set; }
    public int? HouseholdId { get; set; }
    public Household? Household { get; set; }
}
