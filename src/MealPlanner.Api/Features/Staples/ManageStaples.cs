using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Staples;

/// <summary>
/// "Har nok" (snooze a suggestion without touching the rhythm) and "altid med" (pin or unpin an item).
/// POST /api/staples/snooze, GET/PUT/DELETE /api/staples/pinned.
/// </summary>
public static class ManageStaples
{
    public sealed record SnoozeBody(string Name);

    public sealed record PinBody(string Name, string? DefaultQuantity = null);

    public sealed record PinnedDto(int Id, string Name, string? DefaultQuantity);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/staples/snooze", async Task<Results<Ok<DateOnly>, ProblemHttpResult>> (SnoozeBody body,
            ClaimsPrincipal user, AppDbContext db, TimeProvider clock, ListNotifier notifier, CancellationToken ct) =>
        {
            var key = PurchaseRhythm.Key(body.Name ?? "");
            if (key.Length == 0) return TypedResults.Problem("name is required", statusCode: StatusCodes.Status400BadRequest);
            var householdId = user.GetHouseholdId();
            var today = DanishTime.Today(clock);

            var history = (await db.PurchaseHistory.Where(p => p.HouseholdId == householdId)
                    .Select(p => new { p.ItemName, p.Quantity, p.BoughtAt }).ToListAsync(ct))
                .Where(p => PurchaseRhythm.Key(p.ItemName) == key)
                .Select(p => new PurchaseRhythm.Purchase(p.ItemName, p.Quantity, DanishTime.ToDate(p.BoughtAt)))
                .ToList();
            var until = today.AddDays(PurchaseRhythm.SnoozeDays(PurchaseRhythm.Rhythm(history)));

            var snooze = await db.ItemSnoozes.FirstOrDefaultAsync(s => s.HouseholdId == householdId && s.ItemName == key, ct);
            if (snooze is null) db.ItemSnoozes.Add(new ItemSnooze { HouseholdId = householdId, ItemName = key, Until = until });
            else snooze.Until = until;
            await db.SaveChangesAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);
            return TypedResults.Ok(until);
        });

        app.MapGet("/api/staples/pinned", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var householdId = user.GetHouseholdId();
            return TypedResults.Ok(await db.PinnedItems.Where(p => p.HouseholdId == householdId).OrderBy(p => p.ItemName)
                .Select(p => new PinnedDto(p.Id, p.ItemName, p.DefaultQuantity)).ToListAsync(ct));
        });

        app.MapPut("/api/staples/pinned", async Task<Results<Ok<PinnedDto>, ProblemHttpResult>> (PinBody body,
            ClaimsPrincipal user, AppDbContext db, ListNotifier notifier, CancellationToken ct) =>
        {
            var name = body.Name?.Trim() ?? "";
            if (name.Length is 0 or > 200) return TypedResults.Problem("name is required (max 200 characters)", statusCode: 400);
            var householdId = user.GetHouseholdId();

            var pin = await FindPinnedAsync(db, householdId, name, ct);
            if (pin is null)
            {
                pin = new PinnedItem { HouseholdId = householdId, ItemName = name };
                db.PinnedItems.Add(pin);
            }
            pin.DefaultQuantity = string.IsNullOrWhiteSpace(body.DefaultQuantity) ? null : body.DefaultQuantity.Trim();
            await db.SaveChangesAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);
            return TypedResults.Ok(new PinnedDto(pin.Id, pin.ItemName, pin.DefaultQuantity));
        });

        app.MapDelete("/api/staples/pinned", async Task<Results<NoContent, NotFound>> (string name, ClaimsPrincipal user,
            AppDbContext db, ListNotifier notifier, CancellationToken ct) =>
        {
            var householdId = user.GetHouseholdId();
            var pin = await FindPinnedAsync(db, householdId, name, ct);
            if (pin is null) return TypedResults.NotFound();
            db.PinnedItems.Remove(pin);
            await db.SaveChangesAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);
            return TypedResults.NoContent();
        });
    }

    /// <summary>Pinned names are compared normalised, so "Mælk" and "mælk" are the same pin.</summary>
    private static async Task<PinnedItem?> FindPinnedAsync(AppDbContext db, int householdId, string name, CancellationToken ct)
    {
        var key = PurchaseRhythm.Key(name);
        var pins = await db.PinnedItems.Where(p => p.HouseholdId == householdId).ToListAsync(ct);
        return pins.FirstOrDefault(p => PurchaseRhythm.Key(p.ItemName) == key);
    }
}
