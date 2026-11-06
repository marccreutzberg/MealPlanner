namespace MealPlanner.Api.Domain;

/// <summary>A grocery chain as known by Tjek. Shared reference data.</summary>
public class Store
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string TjekDealerId { get; set; } = "";
    public string Color { get; set; } = "";
}
