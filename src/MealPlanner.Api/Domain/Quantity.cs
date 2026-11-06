using System.Globalization;
using System.Text.RegularExpressions;

namespace MealPlanner.Api.Domain;

/// <summary>
/// Amounts as people write them ("600 g", "2 dl", "3 stk.", "1 dåse") and how they add up across recipes
/// (spec 5.5: smør 600 g + 250 g + 175 g = ca. 1 kg). Text that is not an amount ("efter smag") is kept as text.
/// </summary>
public static partial class Quantity
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    private enum Kind { Mass, Volume, Count }

    private sealed record Unit(Kind Kind, decimal Factor, string Singular, string Plural);

    // Factor to the base unit: grams, millilitres, or 1 for counted units (which only add up with themselves).
    private static readonly Dictionary<string, Unit> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["g"] = new(Kind.Mass, 1, "g", "g"),
        ["gram"] = new(Kind.Mass, 1, "g", "g"),
        ["kg"] = new(Kind.Mass, 1000, "g", "g"),
        ["ml"] = new(Kind.Volume, 1, "ml", "ml"),
        ["cl"] = new(Kind.Volume, 10, "ml", "ml"),
        ["dl"] = new(Kind.Volume, 100, "ml", "ml"),
        ["l"] = new(Kind.Volume, 1000, "ml", "ml"),
        ["liter"] = new(Kind.Volume, 1000, "ml", "ml"),
        ["tsk"] = new(Kind.Volume, 5, "ml", "ml"),
        ["spsk"] = new(Kind.Volume, 15, "ml", "ml"),
        ["stk"] = new(Kind.Count, 1, "stk.", "stk."),
        ["stk."] = new(Kind.Count, 1, "stk.", "stk."),
        ["dåse"] = new(Kind.Count, 1, "dåse", "dåser"),
        ["dåser"] = new(Kind.Count, 1, "dåse", "dåser"),
        ["pakke"] = new(Kind.Count, 1, "pakke", "pakker"),
        ["pakker"] = new(Kind.Count, 1, "pakke", "pakker"),
        ["pk"] = new(Kind.Count, 1, "pakke", "pakker"),
        ["pk."] = new(Kind.Count, 1, "pakke", "pakker"),
        ["bakke"] = new(Kind.Count, 1, "bakke", "bakker"),
        ["bakker"] = new(Kind.Count, 1, "bakke", "bakker"),
        ["pose"] = new(Kind.Count, 1, "pose", "poser"),
        ["poser"] = new(Kind.Count, 1, "pose", "poser"),
        ["fed"] = new(Kind.Count, 1, "fed", "fed"),
        ["flaske"] = new(Kind.Count, 1, "flaske", "flasker"),
        ["flasker"] = new(Kind.Count, 1, "flaske", "flasker"),
    };

    /// <summary>A parsed amount in base units, or null for text that is not an amount.</summary>
    private sealed record Amount(decimal Value, Unit Unit, bool Approximate);

    /// <summary>
    /// Adds two amounts. Same kind of unit: one summed amount ("ca. 1 kg"). Otherwise both are kept ("2 dl + 1 stk.").
    /// </summary>
    public static string? Combine(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first)) return Blank(second);
        if (string.IsNullOrWhiteSpace(second)) return Blank(first);

        // Each side may already be a list of amounts ("2 dl + 1 stk."); merge part by part.
        var parts = Split(first).ToList();
        foreach (var part in Split(second))
        {
            var amount = Parse(part);
            var index = amount is null ? -1 : parts.FindIndex(p => Parse(p) is { } existing && SameKind(existing, amount));
            if (index < 0)
            {
                parts.Add(part);
                continue;
            }
            var existing = Parse(parts[index])!;
            parts[index] = Format(existing with { Value = existing.Value + amount!.Value, Approximate = existing.Approximate || amount.Approximate });
        }
        return string.Join(" + ", parts);
    }

    /// <summary>Sums many amounts, e.g. one ingredient across a week of recipes.</summary>
    public static string? Sum(IEnumerable<string?> amounts) => amounts.Aggregate((string?)null, Combine);

    private static IEnumerable<string> Split(string text) =>
        text.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool SameKind(Amount a, Amount b) =>
        a.Unit.Kind == b.Unit.Kind && (a.Unit.Kind != Kind.Count || a.Unit.Singular == b.Unit.Singular);

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static Amount? Parse(string text)
    {
        var match = AmountPattern().Match(text.Trim());
        if (!match.Success) return null;

        var approximate = match.Groups["ca"].Success;
        var number = ParseNumber(match.Groups["number"].Value);
        if (number is null) return null;

        var unitText = match.Groups["unit"].Value;
        if (unitText.Length == 0) return new Amount(number.Value, Units["stk"], approximate);
        return Units.TryGetValue(unitText, out var unit) ? new Amount(number.Value * unit.Factor, unit, approximate) : null;
    }

    private static decimal? ParseNumber(string text)
    {
        if (text == "½") return 0.5m;
        if (text.EndsWith('½') && decimal.TryParse(text[..^1], NumberStyles.Number, Danish, out var whole)) return whole + 0.5m;
        if (text.Contains('/'))
        {
            var fraction = text.Split('/');
            return decimal.TryParse(fraction[0], out var n) && decimal.TryParse(fraction[1], out var d) && d != 0 ? n / d : null;
        }
        return decimal.TryParse(text.Replace('.', ','), NumberStyles.Number, Danish, out var value) ? value : null;
    }

    private static string Format(Amount amount)
    {
        var (value, unit, approximate) = amount;
        string text;
        switch (amount.Unit.Kind)
        {
            case Kind.Mass when value >= 1000:
                text = Number(Round(value / 1000, out var roundedKg), "kg");
                approximate |= roundedKg;
                break;
            case Kind.Mass:
                text = Number(value, "g");
                break;
            case Kind.Volume when value >= 1000:
                text = Number(Round(value / 1000, out var roundedL), "l");
                approximate |= roundedL;
                break;
            case Kind.Volume when value % 100 == 0:
                text = Number(value / 100, "dl");
                break;
            case Kind.Volume:
                text = Number(value, "ml");
                break;
            default:
                text = Number(value, value == 1 ? unit.Singular : unit.Plural);
                break;
        }
        return approximate ? "ca. " + text : text;
    }

    /// <summary>Kilos and litres to the nearest half; a rounded result is marked "ca.".</summary>
    private static decimal Round(decimal value, out bool rounded)
    {
        var result = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2;
        if (result == 0) result = Math.Round(value, 1);
        rounded = result != value;
        return result;
    }

    private static string Number(decimal value, string unit) => value.ToString("0.##", Danish) + " " + unit;

    [GeneratedRegex(@"^(?<ca>ca\.?\s+)?(?<number>\d+(?:[.,]\d+)?½?|½|\d+/\d+)\s*(?<unit>[\p{L}.]+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex AmountPattern();
}
