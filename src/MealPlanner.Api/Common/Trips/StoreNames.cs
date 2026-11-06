using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Results;

namespace MealPlanner.Api.Common.Trips;

/// <summary>Store names from Claude ("Lidl", "rema") to the household's store ids, matched like the shopping list does.</summary>
public static class StoreNames
{
    public static async Task<Result<List<int>>> ResolveAsync(AppDbContext db, int householdId, IEnumerable<string> names,
        CancellationToken ct)
    {
        var stores = await ShoppingListViews.LoadStoresAsync(db, householdId, ct);
        var ids = new List<int>();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var store = stores.FirstOrDefault(s => s.Name.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (store is null)
                return Error.Invalid($"Unknown store '{name}'. Stores: {string.Join(", ", stores.Select(s => s.Name))}");
            ids.Add(store.Id);
        }
        return ids;
    }
}
