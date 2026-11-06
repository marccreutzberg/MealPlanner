using System.Globalization;
using System.Text;
using Microsoft.Extensions.Localization;

namespace MealPlanner.Api.Common.Stores;

/// <summary>
/// The trip as plain text for Notes or a message (spec 12.6): one heading per store, sub-headings per department in
/// walking order, "- [ ]" checklist lines with the known price and then the tag ("Bagning") last.
/// Texts come from TripTexts.resx.
/// </summary>
public static class TripText
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    public static string Format(StoreSelector.Plan plan, IStringLocalizer<TripTexts> texts)
    {
        var sb = new StringBuilder();
        foreach (var visit in plan.Visits.Where(v => v.Items.Count > 0))
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine(visit.Subtotal > 0
                ? string.Format(Danish, texts["StoreWithTotal"], visit.Store.Name, Money(decimal.Round(visit.Subtotal)))
                : visit.Store.Name);

            // Departments in walking order; items without one last, without a heading.
            foreach (var group in visit.Items
                         .GroupBy(i => (i.Item.CategorySort, i.Item.Category))
                         .OrderBy(g => g.Key.Category is null ? 1 : 0)
                         .ThenBy(g => g.Key.CategorySort))
            {
                if (group.Key.Category is { } category) sb.AppendLine(category);
                foreach (var item in group) sb.AppendLine(Line(item, texts));
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string Line(StoreSelector.PlannedItem planned, IStringLocalizer<TripTexts> texts)
    {
        var item = planned.Item;
        var name = string.IsNullOrWhiteSpace(item.Variant) ? item.Name : $"{item.Name} ({item.Variant})";
        if (!string.IsNullOrWhiteSpace(item.Quantity)) name += ", " + item.Quantity;

        var check = item.IsBought ? "- [x] " : "- [ ] ";
        var tag = string.IsNullOrWhiteSpace(item.Tag) ? "" : " · " + item.Tag;
        if (planned.Offer is null) return check + name + tag;

        var paid = planned.Offer.EffectivePrice;
        var price = planned.Count > 1
            ? string.Format(Danish, texts["PricePerPiece"], Money(paid), Money(planned.LinePrice!.Value))
            : string.Format(Danish, texts["Price"], Money(paid));
        if (planned.Offer.AppLabel is { } app) price += $" ({app})";
        return $"{check}{name} · {price}{tag}";
    }

    /// <summary>"18" for whole kroner, "9,95" otherwise.</summary>
    public static string Money(decimal amount) =>
        amount == decimal.Truncate(amount) ? amount.ToString("0", Danish) : amount.ToString("0.00", Danish);
}
