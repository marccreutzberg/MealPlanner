namespace MealPlanner.Api.Domain;

public class ShoppingListItem
{
    public int Id { get; set; }
    public int ShoppingListId { get; set; }
    public ShoppingList ShoppingList { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? Quantity { get; set; }

    /// <summary>Brand or variant that narrows the match, e.g. "Castello" or "rigtigt smør".</summary>
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

    public int? OfferId { get; set; }
    public Offer? Offer { get; set; }

    public bool IsBought { get; set; }
    public DateTimeOffset? BoughtAt { get; set; }
    public string? AddedByUserId { get; set; }
    public User? AddedByUser { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}

public enum StoreFlagSource
{
    Manual,
    Offer,
    Preference,
}
