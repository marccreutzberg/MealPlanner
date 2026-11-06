namespace MealPlanner.Api.Domain;

/// <summary>One bought item. The basis for purchase rhythm suggestions.</summary>
public class PurchaseHistoryEntry
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string ItemName { get; set; } = "";
    public string? Quantity { get; set; }
    public int? StoreId { get; set; }
    public Store? Store { get; set; }
    public decimal? Price { get; set; }
    public DateTimeOffset BoughtAt { get; set; }
}
