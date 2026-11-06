namespace MealPlanner.Api.Domain;

/// <summary>An offer fetched from Tjek. Shared reference data, identified by the Tjek offer id.</summary>
public class Offer
{
    public int Id { get; set; }
    public string TjekId { get; set; } = "";
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public string Heading { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? PrePrice { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }

    /// <summary>Very long validity (e.g. Lidl "Fast lav pris"): an everyday price, not a campaign.</summary>
    public bool IsEverydayPrice { get; set; }

    public bool RequiresAppOrMembership { get; set; }
    public int? MaxPerCustomer { get; set; }

    /// <summary>Set when a person mapped the offer; the fetch job never overwrites the mapping.</summary>
    public bool IsManuallyMapped { get; set; }

    public List<OfferIngredient> Ingredients { get; set; } = [];
}
