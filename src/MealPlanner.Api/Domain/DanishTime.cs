namespace MealPlanner.Api.Domain;

/// <summary>All validity checks are done in Danish local time; Tjek delivers UTC.</summary>
public static class DanishTime
{
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public static DateOnly ToDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    public static DateOnly Today(TimeProvider clock) => ToDate(clock.GetUtcNow());
}
