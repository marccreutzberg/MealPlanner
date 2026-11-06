namespace MealPlanner.Api.Domain;

/// <summary>
/// The unit everything personal belongs to. Location and radius decide which offers are fetched.
/// </summary>
public class Household
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; }

    /// <summary>A third store is only added to a trip when it saves more than this amount (DKK).</summary>
    public decimal ThirdStoreThreshold { get; set; }

    public List<HouseholdStore> Stores { get; set; } = [];
}
