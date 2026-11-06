namespace MealPlanner.Api.Domain;

/// <summary>Many-to-many: one offer heading often covers several ingredients.</summary>
public class OfferIngredient
{
    public int OfferId { get; set; }
    public Offer Offer { get; set; } = null!;
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
}
