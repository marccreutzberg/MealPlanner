namespace MealPlanner.Api.Domain;

public class Recipe
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Servings { get; set; }
    public int TotalMinutes { get; set; }
    public bool IsChildFriendly { get; set; }
    public string? Method { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}
