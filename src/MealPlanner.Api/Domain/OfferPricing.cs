using System.Globalization;
using System.Text.RegularExpressions;

namespace MealPlanner.Api.Domain;

/// <summary>
/// App and membership prices written in the offer text (Lidl Plus, Netto+, Coop and Kvickly members).
/// Tjek has one price per offer; which one it is depends on the chain, so the text decides.
/// </summary>
/// <remarks>
/// Forms seen in November 2026:
/// - Coop/Kvickly "Pris ikke-medlem op til 55,00" (Tjek's price is the member price; "Medlemsspris 99,98" may follow).
/// - Netto "+ PRIS 14:" (the Netto+ price).
/// - Lidl "Med Lidl Plus³ -28 % 20,95" or "Med Lidl Plus³ 19,95" (Tjek's price is the Lidl Plus price, the number after
///   is the normal price) or "-39 %" alone.
/// - Unit prices before and after: Lidl "Pr. l 8,00 … Med Lidl Plus³ Pr. l 6,00", Netto "Pr. liter 60,00 Gælder kun med
///   Netto+ appen Pr. liter 52,00". The app price is the package price scaled by the two unit prices.
/// - Only "Gælder kun med Netto+ appen" or "Medlemspris": the whole offer needs the app; its price is the app price.
/// </remarks>
public static partial class OfferPricing
{
    public sealed record Result(decimal Price, decimal? AppPrice, bool RequiresApp);

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <param name="tjekPrice">The price Tjek delivered.</param>
    public static Result Parse(string text, decimal tjekPrice)
    {
        if (!AppOrMembershipText().IsMatch(text)) return new(tjekPrice, null, false);

        var nonMember = NonMemberPriceText().Match(text);
        if (nonMember.Success)
        {
            var normal = ParseAmount(nonMember.Groups["price"].Value)!.Value;
            var member = MemberPriceText().Match(text);
            var app = member.Success ? ParseAmount(member.Groups["price"].Value)!.Value : tjekPrice;
            return app < normal ? new(normal, app, true) : new(tjekPrice, tjekPrice, true);
        }

        var plus = NettoPlusPriceText().Match(text);
        if (plus.Success && ParseAmount(plus.Groups["price"].Value) is { } nettoPlus && nettoPlus < tjekPrice)
            return new(tjekPrice, nettoPlus, true);

        var lidl = LidlPlusText().Match(text);
        if (lidl.Success)
        {
            var after = text[(lidl.Index + lidl.Length)..];
            var normal = NormalPriceAfterMarkerText().Match(after);
            if (normal.Success && ParseAmount(normal.Groups["price"].Value) is { } normalPrice && normalPrice > tjekPrice)
                return new(normalPrice, tjekPrice, true);

            var percent = PercentAfterMarkerText().Match(after);
            if (percent.Success && int.Parse(percent.Groups["percent"].Value, Invariant) is > 0 and < 100 and var off)
                return new(decimal.Round(tjekPrice / (1 - off / 100m), 2), tjekPrice, true);
        }

        // Two unit prices for the same unit: the lower one is the app price per unit.
        var units = UnitPriceText().Matches(text)
            .Select(m => (Unit: m.Groups["unit"].Value.ToLowerInvariant().TrimEnd('.'), Price: ParseAmount(m.Groups["price"].Value)))
            .Where(u => u.Price is > 0)
            .ToList();
        for (var i = 0; i + 1 < units.Count; i++)
        {
            var (unit, normalUnit) = units[i];
            var app = units.Skip(i + 1).FirstOrDefault(u => SameUnit(u.Unit, unit));
            if (app.Price is { } appUnit && appUnit < normalUnit)
                return new(tjekPrice, decimal.Round(tjekPrice * appUnit / normalUnit!.Value, 2), true);
        }

        // Nothing else: the offer only applies with the app, at Tjek's price.
        return new(tjekPrice, tjekPrice, true);
    }

    /// <summary>"20.53", "14,95", "10,-" -> decimal.</summary>
    public static decimal? ParseAmount(string text)
    {
        var cleaned = text.Trim().TrimEnd('-', '.', ',', ':').Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, Invariant, out var value) ? value : null;
    }

    private static bool SameUnit(string a, string b) => Canonical(a) == Canonical(b);

    private static string Canonical(string unit) => unit switch
    {
        "liter" or "ltr" or "l" => "l",
        _ => unit,
    };

    // "Gælder kun med Netto+ appen", "Med Lidl Plus³", "Medlemspris", "Pris ikke-medlem", "kun for medlemmer".
    [GeneratedRegex(@"netto\+|lidl\s*plus|med\s+\S+\s+app(en)?\b|medlems{1,2}pris|medlemsrabat|pris\s+ikke-medlem|kun\s+for\s+medlemmer|coop\s+app",
        RegexOptions.IgnoreCase)]
    private static partial Regex AppOrMembershipText();

    [GeneratedRegex(@"pris\s+ikke-medlem\s+(?:op\s+til\s+)?(?<price>\d+(?:[.,]\d{1,2})?)", RegexOptions.IgnoreCase)]
    private static partial Regex NonMemberPriceText();

    [GeneratedRegex(@"medlems{1,2}pris\s+(?<price>\d+(?:[.,]\d{1,2})?)", RegexOptions.IgnoreCase)]
    private static partial Regex MemberPriceText();

    [GeneratedRegex(@"\+\s*pris\s*(?<price>\d+(?:[.,]\d{1,2})?)", RegexOptions.IgnoreCase)]
    private static partial Regex NettoPlusPriceText();

    // "Med Lidl Plus³", "Med Lidl Plus" (not "Pris uden Lidl Plus").
    [GeneratedRegex(@"med\s+lidl\s*plus", RegexOptions.IgnoreCase)]
    private static partial Regex LidlPlusText();

    // Right after the marker, optionally after "-28 %": a plain package price such as "20,95". No letters may come
    // first, so a unit price ("Pr. l 6,00") is never taken for it. "³" is not a decimal digit.
    [GeneratedRegex(@"^[^\p{L}\d]*(?:-\s*\d+\s*%[^\p{L}\d]*)?(?<price>\d+[.,]\d{2})")]
    private static partial Regex NormalPriceAfterMarkerText();

    [GeneratedRegex(@"^[^\p{L}\d]*-\s*(?<percent>\d+)\s*%")]
    private static partial Regex PercentAfterMarkerText();

    // "Pr. l 8,00", "Pr. liter 60,00", "Pr. kg max. 137,50", "Pr. stk. 12,50".
    [GeneratedRegex(@"pr\.?\s*(?<unit>kg|liter|ltr|l|stk\.?)\s+(?:maks\.?\s+|max\.?\s+)?(?<price>\d+(?:[.,]\d{1,2})?)", RegexOptions.IgnoreCase)]
    private static partial Regex UnitPriceText();
}
