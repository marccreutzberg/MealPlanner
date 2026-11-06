namespace MealPlanner.Api.Domain;

/// <summary>
/// "Har nok": a staple suggestion swiped away until a day. Does not touch the purchase history, so the rhythm is kept.
/// ItemName is the normalised name.
/// </summary>
public class ItemSnooze
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string ItemName { get; set; } = "";
    public DateOnly Until { get; set; }

    public bool IsActiveOn(DateOnly day) => day <= Until;
}
