using Microsoft.AspNetCore.Identity;

namespace MealPlanner.Api.Domain;

public class User : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
}
