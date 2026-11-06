using System.Text.RegularExpressions;

namespace MealPlanner.Api.Common.Stores;

/// <summary>
/// Plans the shopping trip (spec 4, "Butiksplan" and 12.5): the home store is always visited, the stop that saves
/// the most becomes number two, and a third stop only comes along when it saves more than the household's threshold.
/// Stores in the same stop group (Lidl and Coop 365 in Odder) are one stop. Stores an item is flagged to are always
/// visited. Pure; no database.
/// </summary>
/// <remarks>
/// Normal prices are unknown (spec 10), so a saving is the pre-price when Tjek has one, otherwise an assumed share
/// of the price. Prices are compared per package as printed; kg prices are not normalised yet.
/// </remarks>
public static partial class StoreSelector
{
    /// <summary>A campaign without a pre-price is assumed to be 20 % below normal: saving = 25 % of the offer price.</summary>
    public const decimal AssumedCampaignSaving = 0.25m;

    /// <summary>An everyday low price (Lidl "Fast lav pris") is assumed to be 10 % below a normal shop.</summary>
    public const decimal AssumedEverydaySaving = 0.10m;

    private const int MaxStops = 3;

    public sealed record Store(int Id, string Name, bool IsHome, string? StopGroup);

    public sealed record Offer(int Id, int StoreId, string Heading, decimal Price, decimal? PrePrice, bool IsEverydayPrice,
        DateOnly LastValidDay, decimal? AppPrice = null, string? AppLabel = null)
    {
        /// <summary>What the household pays: the app or member price when there is one.</summary>
        public decimal EffectivePrice => AppPrice ?? Price;
    }

    public sealed record Item(int Id, string Name, string? Variant, string? Quantity, string? Tag, int? FlaggedStoreId,
        bool IsBought, List<Offer> Offers, string? Category = null, int CategorySort = int.MaxValue);

    public sealed record PlannedItem(Item Item, Offer? Offer, int Count, decimal? LinePrice);

    public sealed record Visit(Store Store, List<PlannedItem> Items)
    {
        public decimal Subtotal => Items.Sum(i => i.LinePrice ?? 0);
    }

    public sealed record Plan(List<Visit> Visits, decimal EstimatedSaving);

    public static Plan Choose(List<Store> stores, List<Item> items, decimal thirdStopThreshold)
    {
        var home = stores.FirstOrDefault(s => s.IsHome) ?? throw new InvalidOperationException("The household has no home store");
        var stops = stores.GroupBy(StopKey).ToDictionary(g => g.Key, g => g.ToList());
        var homeStop = StopKey(home);

        var chosen = new HashSet<string> { homeStop };
        foreach (var item in items.Where(i => i.FlaggedStoreId is not null))
        {
            var flagged = stores.FirstOrDefault(s => s.Id == item.FlaggedStoreId);
            if (flagged is not null) chosen.Add(StopKey(flagged));
        }

        // Second stop: anything that saves money. Third stop: only when worth the extra drive.
        if (chosen.Count < 2) AddBestStop(chosen, stops, items, minimumGain: 0);
        if (chosen.Count < MaxStops) AddBestStop(chosen, stops, items, minimumGain: thirdStopThreshold);

        return DistributeOver(chosen.SelectMany(k => stops[k]).ToList(), home, items);
    }

    /// <summary>
    /// Spreads the items over stores chosen beforehand (a saved trip): each chosen store brings its stop-group
    /// partners, the home store is always there, and an item flagged to a store that is not chosen goes home.
    /// </summary>
    public static Plan Distribute(List<Store> stores, IReadOnlyCollection<int> chosenStoreIds, List<Item> items)
    {
        var home = stores.FirstOrDefault(s => s.IsHome) ?? throw new InvalidOperationException("The household has no home store");
        var visitable = Visitable(stores, chosenStoreIds);
        return DistributeOver([.. stores.Where(s => visitable.Contains(s.Id))], home, items);
    }

    /// <summary>The store ids a choice covers: the chosen stores, their stop-group partners and the home store.</summary>
    public static HashSet<int> Visitable(List<Store> stores, IReadOnlyCollection<int> chosenStoreIds)
    {
        var chosenStops = stores.Where(s => s.IsHome || chosenStoreIds.Contains(s.Id)).Select(StopKey).ToHashSet();
        return [.. stores.Where(s => chosenStops.Contains(StopKey(s))).Select(s => s.Id)];
    }

    private static Plan DistributeOver(List<Store> visitable, Store home, List<Item> items)
    {
        var byStore = visitable.ToDictionary(s => s.Id, _ => new List<PlannedItem>());
        decimal saving = 0;
        foreach (var item in items)
        {
            var (store, offer) = Assign(item, visitable, home);
            var count = PieceCount(item.Quantity);
            byStore[store.Id].Add(new PlannedItem(item, offer, count, offer?.EffectivePrice * count));
            if (offer is not null) saving += Saving(offer) * count;
        }

        // Other stops first (biggest basket first), the home store last, as the family drives it.
        var visits = visitable
            .Select(s => new Visit(s, byStore[s.Id]))
            .Where(v => v.Items.Count > 0 || v.Store.IsHome) // the home store is always on the plan (spec 4)
            .OrderBy(v => v.Store.IsHome)
            .ThenByDescending(v => v.Subtotal)
            .ToList();
        return new Plan(visits, decimal.Round(saving, 2));
    }

    private static void AddBestStop(HashSet<string> chosen, Dictionary<string, List<Store>> stops, List<Item> items, decimal minimumGain)
    {
        var current = Evaluate(chosen, stops, items);
        var best = stops.Keys
            .Where(k => !chosen.Contains(k))
            .Select(k => (Key: k, Gain: Evaluate([.. chosen, k], stops, items) - current))
            .OrderByDescending(x => x.Gain)
            .FirstOrDefault();
        if (best.Key is not null && best.Gain > minimumGain) chosen.Add(best.Key);
    }

    /// <summary>Total estimated saving when shopping in these stops.</summary>
    private static decimal Evaluate(IEnumerable<string> stopKeys, Dictionary<string, List<Store>> stops, List<Item> items)
    {
        var storeIds = stopKeys.SelectMany(k => stops[k]).Select(s => s.Id).ToHashSet();
        return items
            .Where(i => i.FlaggedStoreId is null)
            .Select(i => (Item: i, Offer: i.Offers.Where(o => storeIds.Contains(o.StoreId)).MinBy(o => o.EffectivePrice)))
            .Where(x => x.Offer is not null)
            .Sum(x => Saving(x.Offer!) * PieceCount(x.Item.Quantity));
    }

    private static (Store Store, Offer? Offer) Assign(Item item, List<Store> visitable, Store home)
    {
        if (item.FlaggedStoreId is { } flagged)
        {
            // Choose always visits flagged stores; on a saved trip without that store the item goes home.
            var flaggedStore = visitable.FirstOrDefault(s => s.Id == flagged);
            return flaggedStore is null
                ? (home, null)
                : (flaggedStore, item.Offers.Where(o => o.StoreId == flagged).MinBy(o => o.EffectivePrice));
        }

        var ids = visitable.Select(s => s.Id).ToHashSet();
        var cheapest = item.Offers
            .Where(o => ids.Contains(o.StoreId))
            .OrderBy(o => o.EffectivePrice)
            .ThenBy(o => o.StoreId == home.Id ? 0 : 1)
            .FirstOrDefault();
        return cheapest is null ? (home, null) : (visitable.First(s => s.Id == cheapest.StoreId), cheapest);
    }

    /// <summary>
    /// Pre-price minus what is paid when Tjek has a pre-price; else the price without the app minus the app price;
    /// else an assumed share of the price.
    /// </summary>
    public static decimal Saving(Offer offer)
    {
        var paid = offer.EffectivePrice;
        if (offer.PrePrice is { } pre && pre > paid) return pre - paid;
        if (offer.AppPrice is { } app && app < offer.Price) return offer.Price - app;
        return paid * (offer.IsEverydayPrice ? AssumedEverydaySaving : AssumedCampaignSaving);
    }

    /// <summary>"5 stk." -> 5, "2 bakker" -> 2, "3" -> 3. Weights and volumes ("1 kg", "500 g") count as one package.</summary>
    public static int PieceCount(string? quantity)
    {
        if (string.IsNullOrWhiteSpace(quantity)) return 1;
        var match = Pieces().Match(quantity.Trim());
        return match.Success && int.TryParse(match.Groups[1].Value, out var n) && n is > 0 and < 100 ? n : 1;
    }

    private static string StopKey(Store store) => store.StopGroup ?? "store-" + store.Id;

    [GeneratedRegex(@"^(\d+)\s*(stk\.?|bakker?|pk\.?|pakker?|poser?|flasker?|dåser?|ruller?|glas)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Pieces();
}
