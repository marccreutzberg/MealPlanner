namespace MealPlanner.Api.Domain;

/// <summary>An item that is always suggested ("altid med").</summary>
public class PinnedItem
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string ItemName { get; set; } = "";
    public string? DefaultQuantity { get; set; }
}
