using System.Text.RegularExpressions;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Offers;

/// <summary>
/// The offers valid on one day in the household's active stores, and which of them fit a shopping item.
/// Shared by the shopping list, the trip planner and the MCP tools. Load one per request.
/// </summary>
public sealed partial class OfferLookup
{
    public sealed record CurrentOffer(
        int Id, int StoreId, string StoreName, string Heading, string? Description, decimal Price, decimal? PrePrice,
        bool IsEverydayPrice, bool RequiresAppOrMembership, DateOnly LastValidDay, List<int> IngredientIds,
        decimal? AppPrice = null)
    {
        /// <summary>What the household pays: the app price when there is one (they use the apps).</summary>
        public decimal EffectivePrice => AppPrice ?? Price;

        /// <summary>"Lidl Plus", "Netto+" or "medlemspris" when <see cref="EffectivePrice"/> is an app price.</summary>
        public string? AppLabel => AppPrice is null ? null : AppPrices.Label(StoreName);

        internal string SearchText { get; } = IngredientMatcher.Normalize(Heading + " " + Description);

        internal PackageSize? Size { get; } = PackageSize.Parse(Heading) ?? PackageSize.Parse(Description);
    }

    // Words in a variant that say nothing about the product itself.
    private static readonly HashSet<string> VariantFillers = ["rigtigt", "rigtig", "ægte", "helst", "gerne", "til", "med", "fra"];

    // Products nobody means by a plain "Faxe Kondi" or "juice". Checked in the part of the heading that names the
    // item, so "Coca Cola, Fanta, … el. Monster energidrik" still fits Fanta.
    private static readonly string[] SpecialProductWords = ["booster", "energidrik", "energy"];

    // Said in the description: never for a family shopping list unless the item asks for it.
    private const string NotForChildren = "ikke egnet til børn";

    private readonly List<CurrentOffer> offers;

    public OfferLookup(List<CurrentOffer> offers) => this.offers = offers;

    public static Task<OfferLookup> LoadAsync(AppDbContext db, int householdId, DateOnly day, CancellationToken ct) =>
        LoadAsync(db, householdId, day, day, ct);

    /// <summary>Offers valid on at least one day from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static async Task<OfferLookup> LoadAsync(AppDbContext db, int householdId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = DanishTime.StartOf(from);
        var end = DanishTime.StartOf(to.AddDays(1));
        var storeIds = db.HouseholdStores.Where(h => h.HouseholdId == householdId && h.IsActive).Select(h => h.StoreId);

        var rows = await db.Offers.AsNoTracking()
            .Where(o => storeIds.Contains(o.StoreId) && o.ValidFrom < end && o.ValidTo > start)
            .Select(o => new
            {
                o.Id, o.StoreId, StoreName = o.Store.Name, o.Heading, o.Description, o.Price, o.PrePrice,
                o.IsEverydayPrice, o.RequiresAppOrMembership, o.ValidTo, o.AppPrice,
                IngredientIds = o.Ingredients.Select(i => i.IngredientId).ToList(),
            })
            .ToListAsync(ct);

        return new OfferLookup([.. rows.Select(r => new CurrentOffer(r.Id, r.StoreId, r.StoreName, r.Heading, r.Description,
            r.Price, r.PrePrice, r.IsEverydayPrice, r.RequiresAppOrMembership, DanishTime.ToDate(r.ValidTo.AddSeconds(-1)),
            r.IngredientIds, r.AppPrice))]);
    }

    /// <summary>
    /// Offers for an item, cheapest first. Items with an ingredient use its offers; other items match their name
    /// against the heading the same way ingredients do ("Faxe Kondi"). A variant narrows the result (spec 4).
    /// </summary>
    /// <remarks>
    /// Only the product counts: "Juice til børn" is matched as "juice", "Bland selv-slik" as "bland selv slik".
    /// Boosters, energy drinks and offers "ikke egnet til børn" are left out unless the item names them. When the
    /// amount is a size ("1,5 l"), offers whose package fits come first, then those without a known size.
    /// </remarks>
    public List<CurrentOffer> ForItem(int? ingredientId, string name, string? variant, string? quantity = null)
    {
        var itemText = IngredientMatcher.Normalize(name + " " + variant);
        IEnumerable<CurrentOffer> candidates;
        if (ingredientId is { } id)
        {
            candidates = offers.Where(o => o.IngredientIds.Contains(id));
        }
        else
        {
            var wanted = CoreName(name);
            candidates = wanted.Length == 0 ? [] : offers.Where(o => NamedPart(wanted, o.Heading) is { } part
                && !SpecialProductWords.Any(w => part.Contains(w, StringComparison.Ordinal) && !itemText.Contains(w, StringComparison.Ordinal)));
        }

        var (required, excluded) = ParseVariant(variant);
        var wantedSize = PackageSize.Parse(quantity);
        return [.. candidates
            .Where(o => required.Count == 0 || required.Any(w => o.SearchText.Contains(w, StringComparison.Ordinal)))
            .Where(o => !excluded.Any(w => o.SearchText.Contains(w, StringComparison.Ordinal)))
            .Where(o => !o.SearchText.Contains(NotForChildren, StringComparison.Ordinal)
                || SpecialProductWords.Any(w => itemText.Contains(w, StringComparison.Ordinal)))
            .OrderBy(o => SizeRank(o.Size, wantedSize))
            .ThenBy(o => o.EffectivePrice)];
    }

    /// <summary>0: the package fits the wanted size; 1: unknown; 2: a different size.</summary>
    private static int SizeRank(PackageSize? offer, PackageSize? wanted) =>
        wanted is not { } w || offer is not { } o ? 1 : o.Fits(w) ? 0 : 2;

    /// <summary>
    /// The product without who or when it is for: "juice til børn" -> "juice", "deo til pia" -> "deo",
    /// "juice hvis vi mangler" -> "juice". Dashes count as spaces ("selv-slik" = "selv slik").
    /// </summary>
    internal static string CoreName(string name)
    {
        var normalized = IngredientMatcher.Normalize(name).Replace('-', ' ');
        var suffix = ForWhomSuffix().Match(normalized);
        var core = suffix.Success && suffix.Index > 0 ? normalized[..suffix.Index] : normalized;
        return Spaces().Replace(core, " ").Trim();
    }

    /// <summary>The part of a heading ("a, b eller c") that names the item, or null when none does.</summary>
    private static string? NamedPart(string coreName, string heading)
    {
        var normalized = IngredientMatcher.Normalize(heading).Replace('-', ' ');
        return NameAppearsIn(coreName, normalized)
            ? HeadingParts().Split(normalized).FirstOrDefault(p => NameAppearsIn(coreName, p)) ?? normalized
            : null;
    }

    public CurrentOffer? Find(int offerId) => offers.FirstOrDefault(o => o.Id == offerId);

    /// <summary>
    /// The item name as whole words, or as the last part of a compound ("ost" in "rygeost") — never the first part:
    /// in Danish the last part says what the product is, so "kanel" must not match "kanelsnegle".
    /// </summary>
    internal static bool NameAppearsIn(string name, string heading)
    {
        for (var at = heading.IndexOf(name, StringComparison.Ordinal); at >= 0;
             at = heading.IndexOf(name, at + 1, StringComparison.Ordinal))
        {
            var end = at + name.Length;
            var endsWord = end == heading.Length || !char.IsLetterOrDigit(heading[end]);
            var startsWord = at == 0 || !char.IsLetterOrDigit(heading[at - 1]);
            var isCompoundTail = !name.Contains(' ') && name.Length >= 3;
            if (endsWord && (startsWord || isCompoundTail)) return true;
        }
        return false;
    }

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

    // " til børn", " til pia", " hvis vi mangler", " hvis nødvendigt": who or when, not what.
    [GeneratedRegex(@"\s+(?:til|hvis)\s+.*$")]
    private static partial Regex ForWhomSuffix();

    // The separators IngredientMatcher uses between products in one heading.
    [GeneratedRegex(@"\s*,\s*|\s+eller\s+|\s+el\.\s+|\s*/\s*")]
    private static partial Regex HeadingParts();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
