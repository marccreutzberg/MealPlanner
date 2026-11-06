using MealPlanner.Api.Common.Stores;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static MealPlanner.Api.Common.Stores.StoreSelector;

namespace MealPlanner.Tests.Common;

/// <summary>The trip from spec 12.6 (Friday 6/11 2026, birthday and two cakes) as acceptance test.</summary>
public class StoreSelectorTests
{
    private static readonly Store Rema = new(1, "REMA 1000", true, null);
    private static readonly Store Netto = new(2, "Netto", false, null);
    private static readonly Store Kvickly = new(3, "Kvickly", false, null);
    private static readonly Store Lidl = new(4, "Lidl", false, "Lidl og Coop 365");
    private static readonly Store Coop365 = new(5, "Coop 365", false, "Lidl og Coop 365");
    private static readonly List<Store> Stores = [Rema, Netto, Kvickly, Lidl, Coop365];

    private static readonly DateOnly Friday = new(2026, 11, 6);
    private static int nextOfferId = 1;
    private static int nextItemId = 1;

    private static Offer At(Store store, decimal price, bool everyday = false) =>
        new(nextOfferId++, store.Id, "", price, null, everyday, Friday);

    private static Item I(string name, string? quantity = null, string? variant = null, string? tag = null,
        int? flag = null, params Offer[] offers) =>
        new(nextItemId++, name, variant, quantity, tag, flag, false, [.. offers]);

    private static List<Item> BirthdayList() =>
    [
        I("Smør", "5 stk.", "Milbona", offers: [At(Lidl, 9.95m, everyday: true)]),
        I("Sukker", "1 kg", offers: [At(Lidl, 7.50m, everyday: true)]),
        I("Hvedemel", "2 kg", offers: [At(Lidl, 9.95m, everyday: true)]),
        I("Hamburgerryg", variant: "pålæg, Pålægsslagteren", offers: [At(Lidl, 13.95m)]),
        I("Juice til børn", variant: "Capri-Sun 10-pak", offers: [At(Coop365, 25m)]),
        I("Fanta", "1,5 l", offers: [At(Coop365, 10m), At(Netto, 11m)]),
        I("Mælk", variant: "Änglamark", offers: [At(Coop365, 12m)]),
        I("Æg", "2 bakker", tag: "Bagning", offers: [At(Rema, 18m)]),
        I("Marcipan", variant: "Odense", tag: "Bagning", offers: [At(Rema, 29m)]),
        I("Kærnemælk", "1 l", tag: "Bagning"),
        I("Flormelis", "1 kg", tag: "Bagning"),
        I("Castello havarti", tag: "Fødselsdag og resten", offers: [At(Rema, 25m), At(Kvickly, 27m)]),
        I("Faxe Kondi", "1,5 l", tag: "Fødselsdag og resten", offers: [At(Rema, 10m)]),
        I("Spegepølse", tag: "Fødselsdag og resten"),
    ];

    [Fact]
    public void Home_store_plus_the_lidl_coop_stop_like_spec_12_6()
    {
        var plan = Choose(Stores, BirthdayList(), thirdStopThreshold: 50m);

        Assert.Equal(["Lidl", "Coop 365", "REMA 1000"], plan.Visits.Select(v => v.Store.Name));
        Assert.Equal(81.15m, plan.Visits[0].Subtotal); // 5 x 9,95 + 7,50 + 9,95 + 13,95
        Assert.Contains(plan.Visits[2].Items, i => i.Item.Name == "Kærnemælk" && i.Offer is null);
        // Fanta is cheaper at Coop 365 (same stop as Lidl) than at Netto, which is not visited.
        Assert.Contains(plan.Visits[1].Items, i => i.Item.Name == "Fanta");
    }

    [Fact]
    public void Without_offers_elsewhere_everything_stays_in_the_home_store()
    {
        var plan = Choose(Stores, [I("Kærnemælk"), I("Æg", offers: [At(Rema, 18m)])], 50m);

        var visit = Assert.Single(plan.Visits);
        Assert.Equal("REMA 1000", visit.Store.Name);
    }

    [Fact]
    public void Third_stop_only_when_it_saves_more_than_the_threshold()
    {
        var items = new List<Item>
        {
            I("Kaffe", offers: [At(Netto, 100m)]),     // saves 25 kr. -> Netto becomes stop 2
            I("Vaskemiddel", offers: [At(Kvickly, 120m)]), // saves 30 kr. < 50 -> no third stop
        };
        Assert.Equal(["Kvickly", "REMA 1000"], Choose(Stores, items, 50m).Visits.Select(v => v.Store.Name));

        items.Add(I("Bleer", offers: [At(Netto, 300m)]));   // Netto now saves 100 kr. -> stop 2
        items.Add(I("Ost", offers: [At(Kvickly, 120m)]));   // Kvickly now saves 60 kr. > 50 -> stop 3
        Assert.Equal(["Netto", "Kvickly", "REMA 1000"], Choose(Stores, items, 50m).Visits.Select(v => v.Store.Name));
    }

    [Fact]
    public void Flag_always_wins_and_brings_its_store_along()
    {
        var items = new List<Item>
        {
            I("Kakao", flag: Kvickly.Id),
            I("Smør", offers: [At(Lidl, 9.95m, everyday: true)]),
        };
        var plan = Choose(Stores, items, 50m);

        Assert.Contains(plan.Visits, v => v.Store == Kvickly && v.Items.Any(i => i.Item.Name == "Kakao"));
    }

    [Fact]
    public void Distribute_uses_only_chosen_stores_plus_home()
    {
        var items = new List<Item>
        {
            I("Kaffe", offers: [At(Netto, 40m)]),
            I("Smør", offers: [At(Lidl, 9.95m)]),
        };

        var plan = Distribute(Stores, [Netto.Id], items);

        Assert.Equal(["Netto", "REMA 1000"], plan.Visits.Select(v => v.Store.Name));
        Assert.Contains(plan.Visits[1].Items, i => i.Item.Name == "Smør" && i.Offer is null);
    }

    [Fact]
    public void Distribute_treats_stop_group_as_one_stop()
    {
        var plan = Distribute(Stores, [Lidl.Id], [I("Fanta", offers: [At(Coop365, 10m), At(Lidl, 11m)])]);

        Assert.Contains(plan.Visits, v => v.Store == Coop365 && v.Items.Any(i => i.Item.Name == "Fanta"));
    }

    [Fact]
    public void Distribute_sends_flagged_items_home_when_their_store_is_not_chosen()
    {
        var plan = Distribute(Stores, [Netto.Id], [I("Kakao", flag: Kvickly.Id, offers: [At(Kvickly, 30m)])]);

        var visit = Assert.Single(plan.Visits);
        Assert.Equal(Rema, visit.Store);
        Assert.Null(visit.Items.Single().Offer);
    }

    [Fact]
    public void Distribute_moves_items_of_removed_store_home_or_to_cheapest()
    {
        var plan = Distribute(Stores, [Netto.Id], [I("Ost", offers: [At(Kvickly, 10m), At(Netto, 12m)])]);

        var netto = Assert.Single(plan.Visits, v => v.Store == Netto);
        Assert.Equal(12m, netto.Items.Single().Offer!.Price);
    }

    [Fact]
    public void Distribute_with_nothing_chosen_keeps_the_home_store()
    {
        var plan = Distribute(Stores, [], []);

        var visit = Assert.Single(plan.Visits);
        Assert.Equal(Rema, visit.Store);
        Assert.Empty(visit.Items);
    }

    [Theory]
    [InlineData("5 stk.", 5)]
    [InlineData("2 bakker", 2)]
    [InlineData("3", 3)]
    [InlineData("1 kg", 1)]
    [InlineData("500 g", 1)]
    [InlineData(null, 1)]
    public void Piece_count(string? quantity, int expected) => Assert.Equal(expected, PieceCount(quantity));

    [Fact]
    public void Saving_uses_pre_price_when_known()
    {
        Assert.Equal(5m, Saving(new Offer(1, 1, "", 20m, 25m, false, Friday)));
        Assert.Equal(5m, Saving(new Offer(1, 1, "", 20m, null, false, Friday)));    // 25 % assumed
        Assert.Equal(1m, Saving(new Offer(1, 1, "", 10m, null, true, Friday)));     // everyday 10 %
    }

    [Fact]
    public void Text_follows_the_spec_12_6_format()
    {
        var plan = Choose(Stores, BirthdayList(), 50m);
        var texts = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance)
            .Create(typeof(TripTexts));

        var text = TripText.Format(plan, new StringLocalizer<TripTexts>(new FactoryWrapper(texts)));

        Assert.StartsWith("""
            Lidl (ca. 81 kr.)
            - [ ] Smør (Milbona), 5 stk. · 9,95 kr. stk. = 49,75 kr.
            - [ ] Sukker, 1 kg · 7,50 kr.
            - [ ] Hvedemel, 2 kg · 9,95 kr.
            - [ ] Hamburgerryg (pålæg, Pålægsslagteren) · 13,95 kr.
            """.ReplaceLineEndings(), text.ReplaceLineEndings());
        Assert.Contains("""
            Bagning
            - [ ] Æg, 2 bakker · 18 kr. stk. = 36 kr.
            - [ ] Marcipan (Odense) · 29 kr.
            - [ ] Kærnemælk, 1 l
            """.ReplaceLineEndings(), text.ReplaceLineEndings());
    }

    private sealed class FactoryWrapper(IStringLocalizer localizer) : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => localizer;
        public IStringLocalizer Create(string baseName, string location) => localizer;
    }
}
