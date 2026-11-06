namespace MealPlanner.Api.Domain;

/// <summary>
/// A saved shopping trip: the open list items at the time it was made, spread over stores. The tick lives on the
/// list item, so ticking on the trip or on the list is the same thing. At most one active trip per household.
/// </summary>
public class ShoppingTrip
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public decimal EstimatedSaving { get; set; }
    public List<ShoppingTripStore> Stores { get; set; } = [];
    public List<ShoppingTripItem> Items { get; set; } = [];

    public bool IsActive => FinishedAt is null;

    /// <summary>Freezes what each item looked like on the list, so the history does not change afterwards.</summary>
    public void Finish(DateTimeOffset now)
    {
        foreach (var item in Items) item.Freeze();
        FinishedAt = now;
    }
}

/// <summary>A store chosen for the trip. Stores in the same stop group come along with it.</summary>
public class ShoppingTripStore
{
    public int TripId { get; set; }
    public ShoppingTrip Trip { get; set; } = null!;
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
}

/// <summary>
/// One list item on a trip: where to buy it and the offer known when it was placed. Name, amount and the bought
/// state are copied from the list item when the trip ends or the item is removed from the list.
/// </summary>
public class ShoppingTripItem
{
    public int Id { get; set; }
    public int TripId { get; set; }
    public ShoppingTrip Trip { get; set; } = null!;
    public int? ShoppingListItemId { get; set; }
    public ShoppingListItem? ShoppingListItem { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public string Name { get; set; } = "";
    public string? Quantity { get; set; }
    public string? Variant { get; set; }
    public string? Tag { get; set; }

    /// <summary>The list item's category, frozen with the rest, so a finished trip is still sorted by department.</summary>
    public int? CategoryId { get; set; }

    public int? OfferId { get; set; }
    public string? OfferHeading { get; set; }
    public decimal? UnitPrice { get; set; }
    public int Count { get; set; } = 1;
    public decimal? LinePrice { get; set; }
    public DateOnly? LastValidDay { get; set; }

    /// <summary>"Lidl Plus", "Netto+" or "medlemspris" when <see cref="UnitPrice"/> is an app or member price.</summary>
    public string? PriceLabel { get; set; }

    /// <summary>Moved to its store on the trip by hand ("REMA was sold out"); kept there when stores change.</summary>
    public bool MovedByHand { get; set; }

    /// <summary>The bought state frozen when the trip ended or the list item was removed.</summary>
    public bool WasBought { get; set; }

    public void Freeze()
    {
        if (ShoppingListItem is not { } source) return;
        Name = source.Name;
        Quantity = source.Quantity;
        Variant = source.Variant;
        Tag = source.Tag;
        CategoryId = source.CategoryId;
        WasBought = source.IsBought;
    }

    public void PlaceAt(int storeId, int? offerId, string? offerHeading, decimal? unitPrice, int count,
        DateOnly? lastValidDay, bool byHand, string? priceLabel = null)
    {
        StoreId = storeId;
        OfferId = offerId;
        OfferHeading = offerHeading;
        UnitPrice = unitPrice;
        PriceLabel = priceLabel;
        Count = count;
        LinePrice = unitPrice * count;
        LastValidDay = lastValidDay;
        MovedByHand = byHand;
    }
}
