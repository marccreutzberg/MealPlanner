using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Offers;

/// <summary>
/// The offers valid on one day in the household's active stores, and which of them fit a shopping item.
/// Shared by the shopping list, the trip planner and the MCP tools. Load one per request.
/// </summary>
public sealed class OfferLookup
{
    public sealed record CurrentOffer(
        int Id, int StoreId, string StoreName, string Heading, string? Description, decimal Price, decimal? PrePrice,
        bool IsEverydayPrice, bool RequiresAppOrMembership, DateOnly LastValidDay, List<int> IngredientIds)
    {
        internal string SearchText { get; } = IngredientMatcher.Normalize(Heading + " " + Description);
    }

    // Words in a variant that say nothing about the product itself.
    private static readonly HashSet<string> VariantFillers = ["rigtigt", "rigtig", "ægte", "helst", "gerne", "til", "med", "fra"];

    private readonly List<CurrentOffer> offers;

    public OfferLookup(List<CurrentOffer> offers) => this.offers = offers;

    public static async Task<OfferLookup> LoadAsync(AppDbContext db, int householdId, DateOnly day, CancellationToken ct)
    {
        var start = DanishTime.StartOf(day);
        var end = DanishTime.StartOf(day.AddDays(1));
        var storeIds = db.HouseholdStores.Where(h => h.HouseholdId == householdId && h.IsActive).Select(h => h.StoreId);

        var rows = await db.Offers.AsNoTracking()
            .Where(o => storeIds.Contains(o.StoreId) && o.ValidFrom < end && o.ValidTo > start)
            .Select(o => new
            {
                o.Id, o.StoreId, StoreName = o.Store.Name, o.Heading, o.Description, o.Price, o.PrePrice,
                o.IsEverydayPrice, o.RequiresAppOrMembership, o.ValidTo,
                IngredientIds = o.Ingredients.Select(i => i.IngredientId).ToList(),
            })
            .ToListAsync(ct);

        return new OfferLookup([.. rows.Select(r => new CurrentOffer(r.Id, r.StoreId, r.StoreName, r.Heading, r.Description,
            r.Price, r.PrePrice, r.IsEverydayPrice, r.RequiresAppOrMembership, DanishTime.ToDate(r.ValidTo.AddSeconds(-1)),
            r.IngredientIds))]);
    }

    /// <summary>
    /// Offers for an item, cheapest first. Items with an ingredient use its offers; other items match their name
    /// against the heading the same way ingredients do ("Faxe Kondi"). A variant narrows the result (spec 4).
    /// </summary>
    public List<CurrentOffer> ForItem(int? ingredientId, string name, string? variant)
    {
        IEnumerable<CurrentOffer> candidates;
        if (ingredientId is { } id)
        {
            candidates = offers.Where(o => o.IngredientIds.Contains(id));
        }
        else
        {
            var matcher = new IngredientMatcher([new IngredientMatcher.Candidate(0, name, [name], [])]);
            candidates = offers.Where(o => matcher.Match(o.Heading).Count > 0);
        }

        var (required, excluded) = ParseVariant(variant);
        return [.. candidates
            .Where(o => required.Count == 0 || required.Any(w => o.SearchText.Contains(w, StringComparison.Ordinal)))
            .Where(o => !excluded.Any(w => o.SearchText.Contains(w, StringComparison.Ordinal)))
            .OrderBy(o => o.Price)];
    }

    public CurrentOffer? Find(int offerId) => offers.FirstOrDefault(o => o.Id == offerId);

    /// <summary>"rigtigt smør, ikke smørbar" -> required ["smør"], excluded ["smørbar"].</summary>
    internal static (List<string> Required, List<string> Excluded) ParseVariant(string? variant)
    {
        if (string.IsNullOrWhiteSpace(variant)) return ([], []);
        var text = IngredientMatcher.Normalize(variant);
        var split = text.IndexOf("ikke", StringComparison.Ordinal);
        var before = split < 0 ? text : text[..split];
        var after = split < 0 ? "" : text[(split + 4)..];
        return (Words(before), Words(after));

        static List<string> Words(string part) => [.. part
            .Split([' ', ',', '/', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !VariantFillers.Contains(w))];
    }
}
