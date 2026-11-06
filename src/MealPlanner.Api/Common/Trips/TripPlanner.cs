using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Domain;

namespace MealPlanner.Api.Common.Trips;

/// <summary>Translates between list items, the store planner and trip items. Pure; no database.</summary>
public static class TripPlanner
{
    /// <summary>
    /// A list item for <see cref="StoreSelector"/>. An item moved by hand acts as flagged to the store it was moved to,
    /// so redistribution leaves it there.
    /// </summary>
    public static StoreSelector.Item ToPlannerItem(ShoppingListItem item, OfferLookup offers, int? keepAtStoreId) =>
        new(item.Id, item.Name, item.Variant, item.Quantity, item.Tag, keepAtStoreId ?? item.FlaggedStoreId, item.IsBought,
            [.. offers.ForItem(item.IngredientId, item.Name, item.Variant).Select(o => new StoreSelector.Offer(
                o.Id, o.StoreId, o.Heading, o.Price, o.PrePrice, o.IsEverydayPrice, o.LastValidDay, o.AppPrice, o.AppLabel))]);

    /// <summary>Places a trip item where the planner put it, with the offer known now.</summary>
    public static void Apply(ShoppingTripItem target, StoreSelector.PlannedItem planned, int storeId, bool byHand) =>
        target.PlaceAt(storeId, planned.Offer?.Id, planned.Offer?.Heading, planned.Offer?.EffectivePrice, planned.Count,
            planned.Offer?.LastValidDay, byHand, planned.Offer?.AppLabel);
}
