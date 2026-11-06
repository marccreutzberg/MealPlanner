namespace MealPlanner.Api.Common.Offers;

/// <summary>Names of the chains' apps and memberships, for showing an app price ("9 med Lidl Plus").</summary>
public static class AppPrices
{
    public static string Label(string storeName) => storeName switch
    {
        _ when storeName.StartsWith("Lidl", StringComparison.OrdinalIgnoreCase) => "Lidl Plus",
        _ when storeName.StartsWith("Netto", StringComparison.OrdinalIgnoreCase) => "Netto+",
        _ when storeName.StartsWith("Coop", StringComparison.OrdinalIgnoreCase)
            || storeName.StartsWith("Kvickly", StringComparison.OrdinalIgnoreCase) => "medlemspris",
        _ => "app",
    };
}
