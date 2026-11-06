namespace MealPlanner.Api.Domain;

/// <summary>An ingredient line in a recipe. IngredientId is null when the text did not match anything.</summary>
public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public string Text { get; set; } = "";
    public string? Quantity { get; set; }
    public int? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public bool IsMain { get; set; }
}
