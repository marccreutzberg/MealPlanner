namespace MealPlanner.Api.Domain;

/// <summary>
/// A raw ingredient that offers, recipes and shopping items are matched to.
/// Shared when HouseholdId is null.
/// </summary>
public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int? HouseholdId { get; set; }
    public Household? Household { get; set; }
    public List<IngredientAlias> Aliases { get; set; } = [];
}
