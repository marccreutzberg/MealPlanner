namespace MealPlanner.Api.Domain;

/// <summary>
/// Key used by the Claude connector in the X-Api-Key header. Only the hash is stored.
/// </summary>
public class ApiKey
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public User User { get; set; } = null!;
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
