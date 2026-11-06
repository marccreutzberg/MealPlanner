namespace MealPlanner.Api.Domain;

/// <summary>A store as the household uses it: home store, active, and which stop it belongs to.</summary>
public class HouseholdStore
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public bool IsHomeStore { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Stores with the same group are next to each other and count as one stop on a trip.</summary>
    public string? StopGroup { get; set; }
}
