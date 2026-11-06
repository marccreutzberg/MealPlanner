namespace MealPlanner.Api.Domain;

public class ShoppingListItem
{
    public int Id { get; set; }
    public int ShoppingListId { get; set; }
    public ShoppingList ShoppingList { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Quantity { get; set; }

    /// <summary>Brand or variant that narrows the match, e.g. "Castello" or "rigtigt smør, ikke smørbar".</summary>
    public string? Variant { get; set; }

    /// <summary>Groups items on the trip, e.g. "Bagning". Comes from a recipe or is set by hand.</summary>
    public string? Tag { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }

    /// <summary>Store chosen by the trip planner.</summary>
    public int? PlannedStoreId { get; set; }
    public Store? PlannedStore { get; set; }

    /// <summary>Store the item is flagged to. Always wins over the trip planner.</summary>
    public int? FlaggedStoreId { get; set; }
    public Store? FlaggedStore { get; set; }
    public StoreFlagSource? FlagSource { get; set; }

    /// <summary>The offer the item was added from; its last valid day is shown with the flag.</summary>
    public int? OfferId { get; set; }
    public Offer? Offer { get; set; }

    public bool IsBought { get; set; }
    public DateTimeOffset? BoughtAt { get; set; }
    public string? AddedByUserId { get; set; }
    public User? AddedByUser { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    public void FlagTo(int storeId, StoreFlagSource source, int? offerId = null)
    {
        FlaggedStoreId = storeId;
        FlagSource = source;
        OfferId = source == StoreFlagSource.Offer ? offerId : null;
    }

    public void ClearFlag()
    {
        FlaggedStoreId = null;
        FlagSource = null;
        OfferId = null;
    }

    /// <summary>Marks the item bought and returns the purchase to remember for the buying rhythm.</summary>
    public PurchaseHistoryEntry MarkBought(int householdId, int? storeId, decimal? price, DateTimeOffset now)
    {
        IsBought = true;
        BoughtAt = now;
        return new PurchaseHistoryEntry
        {
            HouseholdId = householdId,
            ItemName = Name,
            Quantity = Quantity,
            StoreId = storeId ?? FlaggedStoreId ?? PlannedStoreId,
            Price = price,
            BoughtAt = now,
        };
    }

    public void MarkNotBought()
    {
        IsBought = false;
        BoughtAt = null;
    }

    /// <summary>The same item added twice: keep one line and show both amounts ("2 + 1", "250 g + 175 g").</summary>
    public void AddQuantity(string? quantity)
    {
        if (string.IsNullOrWhiteSpace(quantity)) return;
        Quantity = string.IsNullOrWhiteSpace(Quantity) ? quantity.Trim() : $"{Quantity} + {quantity.Trim()}";
    }
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StoreFlagSource>))]
public enum StoreFlagSource
{
    Manual,
    Offer,
    Preference,
}
