namespace MealPlanner.Api.Domain;

/// <summary>Standing choice, e.g. "bleer køber vi altid i Netto". Applied whenever the item is added.</summary>
public class ItemStorePreference
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string ItemName { get; set; } = "";
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
}
