using System.Text.Json;
using Hangfire;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Tjek;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Offers;

/// <summary>
/// Fetches all offers for the households' active chains from Tjek, upserts them by Tjek id,
/// deletes offers that expired more than 30 days ago and re-matches ingredients (spec 3.1-3.3).
/// </summary>
public sealed class FetchOffersJob(AppDbContext db, TjekClient tjek, TimeProvider clock, ILogger<FetchOffersJob> logger)
{
    private const int KeepExpiredDays = 30;

    public static void Schedule(IRecurringJobManager jobs)
    {
        var options = new RecurringJobOptions { TimeZone = DanishTime.Zone };
        jobs.AddOrUpdate<FetchOffersJob>("fetch-offers-daily", j => j.RunAsync(CancellationToken.None), "0 6 * * *", options);
        // New catalogues start Thursday to Sunday (spec 12.3); fetch again later those mornings.
        jobs.AddOrUpdate<FetchOffersJob>("fetch-offers-catalogue-days", j => j.RunAsync(CancellationToken.None), "30 8 * * 0,4,5,6", options);
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken ct)
    {
        var fetched = await FetchAllAsync(ct);
        var (added, updated, duplicates) = await UpsertAsync(fetched.Offers, ct);

        var cutoff = clock.GetUtcNow().AddDays(-KeepExpiredDays);
        var deleted = await db.Offers.Where(o => o.ValidTo < cutoff).ExecuteDeleteAsync(ct);

        var match = await OfferRematcher.RematchAllAsync(db, ct);
        logger.LogInformation(
            "Offers fetched: {Added} new, {Updated} updated, {Duplicates} duplicates skipped, {Deleted} expired deleted. " +
            "{WithIngredient} of {Total} offers have an ingredient ({Changed} matches changed)",
            added, updated, duplicates, deleted, match.WithIngredient, match.Offers, match.Changed);

        // Saved what we could; still fail the job so a broken chain is visible in Hangfire the same day.
        if (fetched.FailedChains.Count > 0)
            throw new InvalidOperationException("Fetching offers failed for: " + string.Join(", ", fetched.FailedChains));
    }

    private sealed record FetchResult(List<(Store Store, TjekOffer Offer)> Offers, List<string> FailedChains);

    private async Task<FetchResult> FetchAllAsync(CancellationToken ct)
    {
        var households = await db.Households
            .Include(h => h.Stores.Where(s => s.IsActive)).ThenInclude(s => s.Store)
            .AsNoTracking()
            .ToListAsync(ct);

        var offers = new Dictionary<string, (Store, TjekOffer)>();
        var failed = new List<string>();
        foreach (var household in households)
        {
            foreach (var store in household.Stores.Select(s => s.Store))
            {
                try
                {
                    var chainOffers = await tjek.GetOffersAsync(store.TjekDealerId, household.Latitude, household.Longitude,
                        household.RadiusMeters, ct);
                    logger.LogInformation("Tjek returned {Count} offers for {Chain}", chainOffers.Count, store.Name);
                    if (chainOffers.Count == 0)
                    {
                        // A chain never has zero offers in a 20 km radius; most likely the API changed.
                        logger.LogError("Tjek returned no offers for {Chain} ({DealerId})", store.Name, store.TjekDealerId);
                        failed.Add(store.Name);
                    }
                    foreach (var offer in chainOffers)
                        offers[offer.Id] = (store, offer);
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    logger.LogError(ex, "Fetching offers from Tjek failed for {Chain} ({DealerId})", store.Name, store.TjekDealerId);
                    failed.Add(store.Name);
                }
            }
        }
        return new FetchResult([.. offers.Values], failed);
    }

    private async Task<(int Added, int Updated, int Duplicates)> UpsertAsync(List<(Store Store, TjekOffer Offer)> fetched, CancellationToken ct)
    {
        var existing = await db.Offers.ToDictionaryAsync(o => o.TjekId, ct);

        // The same product can appear twice (catalogue and "spot") with different ids (spec 12.1). Tjek's price may
        // have become the app price (Lidl, Coop), so both prices count as the one Tjek delivered.
        var seen = existing.Values
            .SelectMany(o => new[] { o.Price, o.AppPrice ?? o.Price }.Select(p => DuplicateKey(o.StoreId, o.Heading, p, o.ValidFrom)))
            .ToHashSet();

        int added = 0, updated = 0, duplicates = 0;
        foreach (var (store, item) in fetched)
        {
            if (existing.TryGetValue(item.Id, out var offer))
            {
                offer.Update(item.Heading, item.Description, item.Pricing.Price, item.Pricing.PrePrice, item.RunFrom, item.RunTill);
                updated++;
                continue;
            }

            if (!seen.Add(DuplicateKey(store.Id, item.Heading, item.Pricing.Price, item.RunFrom)))
            {
                duplicates++;
                continue;
            }

            offer = new Offer { TjekId = item.Id, StoreId = store.Id };
            offer.Update(item.Heading, item.Description, item.Pricing.Price, item.Pricing.PrePrice, item.RunFrom, item.RunTill);
            db.Offers.Add(offer);
            added++;
        }

        await db.SaveChangesAsync(ct);
        return (added, updated, duplicates);
    }

    private static (int, string, decimal, DateTimeOffset) DuplicateKey(int storeId, string heading, decimal price, DateTimeOffset from) =>
        (storeId, IngredientMatcher.Normalize(heading), price, from);
}
