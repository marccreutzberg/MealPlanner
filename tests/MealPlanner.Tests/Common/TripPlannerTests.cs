using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Common.Trips;
using MealPlanner.Api.Domain;

namespace MealPlanner.Tests.Common;

public class TripPlannerTests
{
    private static readonly DateOnly Thursday = new(2026, 11, 12);

    private static readonly OfferLookup Offers = new(
    [
        new OfferLookup.CurrentOffer(11, StoreId: 2, "Netto", "Kaffe", null, 40m, null, false, false, Thursday, []),
    ]);

    private static readonly ShoppingListItem Kaffe = new() { Id = 5, Name = "Kaffe", Quantity = "2 pk.", FlaggedStoreId = 4 };

    [Fact]
    public void ToPlannerItem_keeps_moved_item_at_its_store()
    {
        var item = TripPlanner.ToPlannerItem(Kaffe, Offers, keepAtStoreId: 3);

        Assert.Equal(3, item.FlaggedStoreId);
        Assert.Equal(40m, Assert.Single(item.Offers).Price);
    }

    [Fact]
    public void ToPlannerItem_uses_list_flag_otherwise()
    {
        Assert.Equal(4, TripPlanner.ToPlannerItem(Kaffe, Offers, keepAtStoreId: null).FlaggedStoreId);
    }

    [Fact]
    public void Apply_copies_offer_snapshot_and_line_price()
    {
        var offer = new StoreSelector.Offer(11, 2, "Smør", 9.95m, null, false, Thursday);
        var planned = new StoreSelector.PlannedItem(TripPlanner.ToPlannerItem(Kaffe, Offers, null), offer, 2, 19.90m);
        var target = new ShoppingTripItem();

        TripPlanner.Apply(target, planned, storeId: 2, byHand: false);

        Assert.Equal((2, 11, "Smør", 9.95m, 19.90m, Thursday), (target.StoreId, target.OfferId, target.OfferHeading, target.UnitPrice, target.LinePrice, target.LastValidDay));
        Assert.False(target.MovedByHand);
    }

    private static ShoppingTripItem Snapshot(ShoppingListItem? live) => new()
    {
        Id = 1, Name = "Kaffe", Quantity = "1 pk.", ShoppingListItem = live, ShoppingListItemId = live?.Id,
    };

    [Fact]
    public void Active_trip_shows_live_list_values()
    {
        var dto = TripViews.ToItem(Snapshot(new ShoppingListItem { Id = 5, Name = "Kaffe", Quantity = "2 pk.", IsBought = true }), active: true);

        Assert.NotNull(dto);
        Assert.Equal(("2 pk.", true, 5), (dto.Quantity, dto.IsBought, dto.ItemId));
    }

    [Fact]
    public void Finished_trip_shows_the_frozen_copy()
    {
        var item = Snapshot(new ShoppingListItem { Id = 5, Name = "Kaffe", Quantity = "2 pk.", IsBought = false });
        item.WasBought = true;

        var dto = TripViews.ToItem(item, active: false);

        Assert.Equal(("1 pk.", true), (dto!.Quantity, dto.IsBought));
    }

    [Fact]
    public void Removed_unbought_item_is_hidden_on_the_active_trip()
    {
        Assert.Null(TripViews.ToItem(Snapshot(null), active: true));
    }

    [Fact]
    public void Removed_bought_item_stays_as_bought()
    {
        var item = Snapshot(null);
        item.WasBought = true;

        var dto = TripViews.ToItem(item, active: true);

        Assert.Equal((true, (int?)null), (dto!.IsBought, dto.ItemId));
    }
}
