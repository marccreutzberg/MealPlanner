using MealPlanner.Api.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Trips;

/// <summary>
/// Call before list items are deleted: items on an active trip are frozen (so a bought item stays bought on the trip),
/// and every trip's link to them is cleared. The database has no cascade rule for this link.
/// </summary>
public static class TripFreezer
{
    /// <param name="listItemIds">
    /// The ids that will be deleted, read once by the caller, so an item ticked on the other phone in between is
    /// neither frozen nor deleted (deleting it would hit the foreign key).
    /// </param>
    public static async Task FreezeAsync(AppDbContext db, IReadOnlyCollection<int> listItemIds, CancellationToken ct)
    {
        if (listItemIds.Count == 0) return;
        var tripItems = await db.ShoppingTripItems
            .Include(t => t.Trip)
            .Include(t => t.ShoppingListItem)
            .Where(t => t.ShoppingListItemId != null && listItemIds.Contains(t.ShoppingListItemId.Value))
            .ToListAsync(ct);
        if (tripItems.Count == 0) return;

        foreach (var item in tripItems)
        {
            // A finished trip is already frozen; the list item may have changed since.
            if (item.Trip.IsActive) item.Freeze();
            item.ShoppingListItem = null;
            item.ShoppingListItemId = null;
        }
        await db.SaveChangesAsync(ct);
    }
}
