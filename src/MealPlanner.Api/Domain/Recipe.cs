namespace MealPlanner.Api.Domain;

/// <summary>A dish the household eats. In v1 Claude writes the recipes (spec 5); names are unique per household.</summary>
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

    /// <summary>Replaces everything but the identity, so Claude can correct a recipe by saving it again.</summary>
    public void Update(string name, int servings, int totalMinutes, bool isChildFriendly, string? method,
        IEnumerable<RecipeIngredient> ingredients)
    {
        Name = name;
        Servings = servings;
        TotalMinutes = totalMinutes;
        IsChildFriendly = isChildFriendly;
        Method = string.IsNullOrWhiteSpace(method) ? null : method.Trim();
        Ingredients.Clear();
        Ingredients.AddRange(ingredients);
    }

    public IEnumerable<RecipeIngredient> MainIngredients => Ingredients.Where(i => i.IsMain);
}
