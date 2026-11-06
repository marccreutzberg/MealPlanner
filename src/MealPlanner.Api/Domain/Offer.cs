using System.Globalization;
using System.Text.RegularExpressions;

namespace MealPlanner.Api.Domain;

/// <summary>An offer fetched from Tjek. Shared reference data, identified by the Tjek offer id.</summary>
public partial class Offer
{
    // Lidl's "Fast lav pris" runs for months; Kvickly's Christmas menus run ~12 weeks and are still campaigns.
    private static readonly TimeSpan EverydayPriceMinimumRun = TimeSpan.FromDays(120);

    public int Id { get; set; }
    public string TjekId { get; set; } = "";
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public string Heading { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? PrePrice { get; set; }

    /// <summary>The price with the chain's app or membership (Lidl Plus, Netto+, Coop); null when there is none.</summary>
    public decimal? AppPrice { get; set; }

    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }

    /// <summary>Everyday price (e.g. Lidl "Fast lav pris"), not a campaign. Used as normal price for the item.</summary>
    public bool IsEverydayPrice { get; set; }

    public bool RequiresAppOrMembership { get; set; }
    public int? MaxPerCustomer { get; set; }

    /// <summary>Set when a person mapped the offer; the fetch job never overwrites the mapping.</summary>
    public bool IsManuallyMapped { get; set; }

    public List<OfferIngredient> Ingredients { get; set; } = [];

    public DateOnly FirstValidDay => DanishTime.ToDate(ValidFrom);

    // Tjek ends offers at 23:59:59 local time, the spec describes local midnight; one second back covers both.
    public DateOnly LastValidDay => DanishTime.ToDate(ValidTo.AddSeconds(-1));

    public bool IsValidOn(DateOnly day) => FirstValidDay <= day && day <= LastValidDay;

    public bool IsValidWithin(DateOnly from, DateOnly to) => FirstValidDay <= to && from <= LastValidDay;

    public bool IsExpiredLongerThan(DateOnly today, int days) => LastValidDay.AddDays(days) < today;

    /// <summary>Copies the fetched values and derives the conditions written in the offer text.</summary>
    public void Update(string heading, string? description, decimal price, decimal? prePrice,
        DateTimeOffset validFrom, DateTimeOffset validTo)
    {
        Heading = heading.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PrePrice = prePrice;
        ValidFrom = validFrom;
        ValidTo = validTo;

        var text = Heading + "\n" + Description;
        IsEverydayPrice = EverydayPriceText().IsMatch(text) || validTo - validFrom >= EverydayPriceMinimumRun;

        // REMA: "Ved køb af flere end 6 stk. pr. variant pr. dag er prisen 20.53 pr. stk." Tjek delivers that price
        // as pre_price, but it is the price above the limit, not a former price.
        int? limit = null;
        var bulk = QuantityLimitText().Match(text);
        if (bulk.Success)
        {
            limit = int.Parse(bulk.Groups["count"].Value, CultureInfo.InvariantCulture);
            if (PrePrice == OfferPricing.ParseAmount(bulk.Groups["price"].Value)) PrePrice = null;
        }
        var max = MaxPerCustomerText().Match(text);
        MaxPerCustomer = max.Success ? int.Parse(max.Groups[1].Value, CultureInfo.InvariantCulture) : limit;

        var pricing = OfferPricing.Parse(text, price);
        Price = pricing.Price;
        AppPrice = pricing.AppPrice;
        RequiresAppOrMembership = pricing.RequiresApp;
    }

    /// <summary>Replaces automatic matches. Ignored once a person has mapped the offer.</summary>
    /// <returns>True when the set of ingredients changed.</returns>
    public bool SetMatchedIngredients(IReadOnlyCollection<int> ingredientIds)
    {
        if (IsManuallyMapped) return false;
        return ReplaceIngredients(ingredientIds);
    }

    /// <summary>A person's mapping. An empty list means "no ingredient" (e.g. non-food that was matched wrongly).</summary>
    public void MapManually(IReadOnlyCollection<int> ingredientIds)
    {
        ReplaceIngredients(ingredientIds);
        IsManuallyMapped = true;
    }

    private bool ReplaceIngredients(IReadOnlyCollection<int> ingredientIds)
    {
        var current = Ingredients.Select(i => i.IngredientId).ToHashSet();
        if (current.SetEquals(ingredientIds)) return false;

        Ingredients.RemoveAll(i => !ingredientIds.Contains(i.IngredientId));
        foreach (var id in ingredientIds.Where(id => !current.Contains(id)))
            Ingredients.Add(new OfferIngredient { IngredientId = id });
        return true;
    }

    [GeneratedRegex(@"fast\s+lav\s+pris", RegexOptions.IgnoreCase)]
    private static partial Regex EverydayPriceText();

    // "Maks. 3 poser pr. kunde", "Max 2 stk. pr. kunde", Netto's "Max. 6 stk. pr. variant pr. kunde pr. dag".
    [GeneratedRegex(@"ma(?:ks|x)\.?\s*(\d+)\s*[\p{L}.]*(?:\s*pr\.?\s*variant)?\s*pr\.?\s*kunde", RegexOptions.IgnoreCase)]
    private static partial Regex MaxPerCustomerText();

    // REMA: "Ved køb af flere end 72 stk. pr. variant pr. dag er prisen op til 7.50 pr. stk."
    [GeneratedRegex(@"flere\s+end\s+(?<count>\d+)\s*stk\.?(?:\s*pr\.?\s*variant)?\s*pr\.?\s*dag\s+er\s+prisen\s+(?:op\s+til\s+)?(?<price>\d+(?:[.,]\d{1,2})?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex QuantityLimitText();
}
