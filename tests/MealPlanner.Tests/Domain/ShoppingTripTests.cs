using MealPlanner.Api.Domain;

namespace MealPlanner.Tests.Domain;

public class ShoppingTripTests
{
    private static readonly DateTimeOffset Sunday = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

    private static ShoppingTripItem OnTrip(ShoppingListItem? listItem) => new()
    {
        Name = "Kaffe", Quantity = "1 pk.", ShoppingListItem = listItem, ShoppingListItemId = listItem?.Id,
    };

    [Fact]
    public void Freeze_copies_list_values_and_bought_state()
    {
        var item = OnTrip(new ShoppingListItem { Id = 7, Name = "Kaffe", Quantity = "2 pk.", Variant = "Merrild", Tag = "Morgen", IsBought = true });

        item.Freeze();

        Assert.Equal("2 pk.", item.Quantity);
        Assert.Equal("Merrild", item.Variant);
        Assert.Equal("Morgen", item.Tag);
        Assert.True(item.WasBought);
    }

    [Fact]
    public void Freeze_without_list_item_keeps_snapshot()
    {
        var item = OnTrip(null);
        item.WasBought = true;

        item.Freeze();

        Assert.Equal("1 pk.", item.Quantity);
        Assert.True(item.WasBought);
    }

    [Fact]
    public void Finish_sets_time_and_freezes_items()
    {
        var trip = new ShoppingTrip
        {
            Items = [OnTrip(new ShoppingListItem { Id = 1, Name = "Kaffe", IsBought = true }), OnTrip(new ShoppingListItem { Id = 2, Name = "Te" })],
        };

        trip.Finish(Sunday);

        Assert.False(trip.IsActive);
        Assert.Equal(Sunday, trip.FinishedAt);
        Assert.Equal([true, false], trip.Items.Select(i => i.WasBought));
        Assert.Equal("Te", trip.Items[1].Name);
    }

    [Fact]
    public void PlaceAt_by_hand_marks_item_moved_and_replaces_the_price()
    {
        var item = OnTrip(null);
        item.PlaceAt(storeId: 3, offerId: 11, offerHeading: "Piskefløde", unitPrice: 12m, count: 2,
            lastValidDay: new DateOnly(2026, 11, 12), byHand: true);

        Assert.Equal(3, item.StoreId);
        Assert.Equal(24m, item.LinePrice);
        Assert.True(item.MovedByHand);

        item.PlaceAt(4, null, null, null, 2, null, byHand: true);
        Assert.Null(item.UnitPrice);
        Assert.Null(item.LinePrice);
        Assert.Null(item.OfferHeading);
    }
}
