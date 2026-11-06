namespace MealPlanner.Api.Domain;

/// <summary>Shopping list category. SortOrder follows the walk through the home store.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public int SortOrder { get; set; }
}
