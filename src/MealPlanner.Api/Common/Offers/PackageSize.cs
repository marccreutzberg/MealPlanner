using System.Globalization;
using System.Text.RegularExpressions;

namespace MealPlanner.Api.Common.Offers;

/// <summary>
/// A package size or range in millilitres or grams, read from an offer ("150 cl", "27,5-33 cl", "1.5 liter",
/// "200-240 g") or a shopping item's amount ("1,5 l").
/// </summary>
public readonly partial record struct PackageSize(bool IsVolume, decimal Min, decimal Max)
{
    // A wanted amount fits a package within this tolerance (a 1,5 l item fits "1,25 l" and "1,75 l" bottles).
    private const decimal Below = 0.8m;
    private const decimal Above = 1.25m;

    public bool Fits(PackageSize wanted) =>
        IsVolume == wanted.IsVolume && wanted.Min >= Min * Below && wanted.Max <= Max * Above;

    /// <summary>The first size in the text, or null. Unit prices ("Pr. kg 49,90", "PR. 1/2 KG") are not sizes.</summary>
    public static PackageSize? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = SizeText().Match(text);
        if (!match.Success) return null;

        var (isVolume, factor) = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "ml" => (true, 1m),
            "cl" => (true, 10m),
            "dl" => (true, 100m),
            "l" or "ltr" or "liter" => (true, 1000m),
            "g" => (false, 1m),
            _ => (false, 1000m), // kg
        };
        var min = Number(match.Groups["min"].Value) * factor;
        var max = match.Groups["max"].Success ? Number(match.Groups["max"].Value) * factor : min;
        return new PackageSize(isVolume, min, max);
    }

    private static decimal Number(string text) => decimal.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(?<![\d/.,])(?<min>\d+(?:[.,]\d+)?)(?:\s*-\s*(?<max>\d+(?:[.,]\d+)?))?\s*(?<unit>ml|cl|dl|ltr|liter|l|kg|g)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SizeText();
}
