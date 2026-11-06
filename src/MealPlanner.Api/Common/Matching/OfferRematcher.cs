using MealPlanner.Api.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Matching;

/// <summary>
/// Re-runs the matcher on every offer that has not been mapped by hand.
/// Used after a fetch and after a manual mapping has added a keyword.
/// </summary>
public static class OfferRematcher
{
    public sealed record Result(int Offers, int Changed, int WithIngredient);

    public static async Task<Result> RematchAllAsync(AppDbContext db, CancellationToken ct)
    {
        var matcher = await IngredientMatcher.LoadAsync(db, householdId: null, ct);
        var offers = await db.Offers
            .Where(o => !o.IsManuallyMapped)
            .Include(o => o.Ingredients)
            .ToListAsync(ct);

        var changed = offers.Count(o => o.SetMatchedIngredients(matcher.Match(o.Heading)));
        await db.SaveChangesAsync(ct);

        var withIngredient = await db.Offers.CountAsync(o => o.Ingredients.Any(), ct);
        return new Result(await db.Offers.CountAsync(ct), changed, withIngredient);
    }
}
