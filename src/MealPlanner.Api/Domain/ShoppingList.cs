namespace MealPlanner.Api.Domain;

/// <summary>A household has one active, shared shopping list.</summary>
public class ShoppingList
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<ShoppingListItem> Items { get; set; } = [];
}
