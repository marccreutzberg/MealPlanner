using System.ComponentModel;
using System.Reflection;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Stores;
using MealPlanner.Api.Common.Trips;
using MealPlanner.Api.Domain;
using MealPlanner.Api.Features.ShoppingList;
using MealPlanner.Api.Mcp;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Offer = MealPlanner.Api.Domain.Offer;
using PlanOffer = MealPlanner.Api.Common.Stores.StoreSelector.Offer;
using PlanItem = MealPlanner.Api.Common.Stores.StoreSelector.Item;
using PlanStore = MealPlanner.Api.Common.Stores.StoreSelector.Store;

namespace MealPlanner.Tests.Regression;

/// <summary>
/// Findings from testing the MCP tools with a real list (birthday and baking, Friday 6/11 2026).
/// The offer texts are copied from Tjek as fetched that day.
/// </summary>
public class ShoppingTestNov6Tests
{
    private static readonly DateOnly Friday = new(2026, 11, 6);

    private static Offer Fetched(string heading, string? description, decimal price, decimal? prePrice = null)
    {
        var offer = new Offer();
        offer.Update(heading, description, price, prePrice,
            DateTimeOffset.Parse("2026-11-04T23:00:00+00:00"), DateTimeOffset.Parse("2026-11-12T22:59:59+00:00"));
        return offer;
    }

    // ---- A. Parsing the offer texts ----------------------------------------------------------------------------

    [Fact]
    public void A1_rema_price_above_the_limit_is_not_a_pre_price()
    {
        // REMA offer 3: Tjek's pre_price is the price for more than six bottles, not a former price.
        var offer = Fetched("Pepsi Max eller Faxe Kondi",
            "1.5 liter. Ex. pant\nVed køb af flere end 6 stk. pr. variant pr. dag er prisen 20.53 pr. stk.\n6.67 pr. liter",
            10m, 20.53m);

        Assert.Null(offer.PrePrice);
        Assert.Equal(6, offer.MaxPerCustomer);
    }

    [Theory]
    [InlineData("Karolines Køkken Piskefløde 36%", "½ liter Ved køb af flere end 6 stk. pr. dag er prisen 14.95 pr. stk.", 14.95, 6)]
    [InlineData("Burgerboost", "Ved køb af flere end 6 stk. pr. dag er prisen 39.95 pr. stk. 2x175 g", 39.95, 6)]
    [InlineData("Tuborg, Carlsberg eller Brewmasters IPA",
        "33 cl. Ex. pant\nVed køb af flere end 72 stk. pr. variant pr. dag er prisen op til 7.50 pr. stk.\nKasse med 24 stk. 96.00/12.12 pr. liter",
        null, 72)]
    public void A1_rema_quantity_limit_variants(string heading, string description, double? prePrice, int max)
    {
        var offer = Fetched(heading, description, 10m, (decimal?)prePrice);

        Assert.Null(offer.PrePrice);
        Assert.Equal(max, offer.MaxPerCustomer);
    }

    [Fact]
    public void A1_a_real_normal_price_is_kept()
    {
        // Netto 289: "Normalpris op til 23,95" is a former price and stays.
        var offer = Fetched("Hellmann's mayonnaise eller Maille dijon sennep",
            "200-215 g.\nPr. kg max. 49,85\nNormalpris op til 23,95\nMax. 6 stk. pr. variant pr. kunde pr. dag til 9,97\nSPAR OP TIL 13,98",
            9.97m, 23.95m);

        Assert.Equal(23.95m, offer.PrePrice);
    }

    public static TheoryData<string, string, decimal, decimal, decimal?> AppPriceOffers => new()
    {
        // Lidl 1455: the Lidl Plus price is a litre price; 1,5 l x 6,00 = 9,00.
        { "FAXE KONDI/PEPSI Sodavand", "Ekskl. pant. 1,5 l. Pr. l 8,00 Maks. 3 stk. pr. kunde pr. dag.\nMed Lidl Plus³\nPr. l 6,00", 12m, 12m, 9m },
        // Lidl 1467: Tjek's price is already the Lidl Plus price; 20,95 after the percentage is the normal price.
        { "K-SALAT Bearnaisesauce", "325 g. Pr. kg 46,15\nMed Lidl Plus³\n-28 %\n20,95", 15m, 20.95m, 15m },
        // Netto 251: "+ PRIS 14:" is the Netto+ price.
        { "Egelykke danske skrabeæg", "10 stk. Str. M/L.\nPr. stk. 1,80\n+ PRIS 14:\nPr. stk. 1,40\nGælder kun med Netto+ appen", 18m, 18m, 14m },
        // Netto 279: the whole offer only applies with the app.
        { "Dansk hakket kyllingekød 3-7%", "1000 g.\nGælder kun med Netto+ appen", 55m, 55m, 55m },
        // Coop 365 2184: Tjek's price is the member price; the non-member price is in the text.
        { "Steff Houlberg pølser eller bacon",
          "500-875 g. Kg-pris maks. 90,00.\n5-pak\nMedlemsrabat op til 10,-\nPris ikke-medlem op til 55,00\nMÅNEDENS Medlems KØB NOVEMBER",
          45m, 55m, 45m },
    };

    [Theory]
    [MemberData(nameof(AppPriceOffers))]
    public void A2_app_and_member_prices(string heading, string description, decimal tjekPrice, decimal price, decimal? appPrice)
    {
        var offer = Fetched(heading, description, tjekPrice);

        Assert.True(offer.RequiresAppOrMembership);
        Assert.Equal(price, offer.Price);
        Assert.Equal(appPrice, offer.AppPrice);
    }

    [Fact]
    public void A2_the_trip_uses_the_app_price_and_names_the_app()
    {
        var lidl = new PlanStore(5, "Lidl", false, null);
        var rema = new PlanStore(1, "REMA 1000", true, null);
        var faxe = new PlanItem(1, "Faxe Kondi", null, "1,5 l", null, lidl.Id, false,
            [new PlanOffer(1455, lidl.Id, "FAXE KONDI/PEPSI Sodavand", 12m, null, false, Friday, 9m, "Lidl Plus")]);

        var plan = StoreSelector.Choose([rema, lidl], [faxe], 50m);

        var planned = plan.Visits.Single(v => v.Store == lidl).Items.Single();
        Assert.Equal(9m, planned.LinePrice);
        Assert.Equal(3m, plan.EstimatedSaving); // 12,00 without the app - 9,00 with it
        Assert.Equal("Lidl Plus", AppPrices.Label("Lidl"));
        Assert.Equal("Netto+", AppPrices.Label("Netto"));
        Assert.Equal("medlemspris", AppPrices.Label("Coop 365"));
    }

    [Theory]
    [InlineData("1,5 liter. + pant.\nPr. liter 6,67\nMax. 6 stk. pr. variant pr. kunde pr. dag til denne pris", 6)] // Netto 262
    [InlineData("1 kg.\nMax. 3 stk. pr. kunde pr. dag til denne pris", 3)] // Netto 257
    [InlineData("380-1000 g\nVed køb af flere end 6 stk. pr. variant pr. dag er prisen op til 26.50 pr. stk.\nMax. 26.32 pr. kg", 6)] // REMA 2
    [InlineData("200-240 g\nMax. 125.00 pr. kg", null)] // REMA 19: a kg price, not a limit
    public void A3_max_per_customer_in_all_chains(string description, int? expected)
    {
        Assert.Equal(expected, Fetched("Vare", description, 10m).MaxPerCustomer);
    }

    // ---- B. Matching shopping items to offers ------------------------------------------------------------------

    private static OfferLookup.CurrentOffer Current(int id, string store, string heading, string? description, decimal price) =>
        new(id, 0, store, heading, description, price, null, false, false, new DateOnly(2026, 11, 12), []);

    private static readonly OfferLookup Today = new(
    [
        Current(730, "Kvickly", "Faxe Kondi Booster, Royal Club eller Shaker",
            "Ikke egnet til børn. Flere varianter. 27,5-33 cl. Literpris maks. 36,36 + pant. 1 flaske/dåse.", 10m),
        Current(780, "Kvickly", "Faxe Kondi eller Pepsi Max", "Flere varianter. 150 cl. Literpris 8,00 + pant. 1 flaske", 12m),
        Current(1225, "Løvbjerg", "Coca Cola, Fanta, Squash, Sprite Zero el. Monster energidrik",
            "Flere varianter\nEnergidrikke er ikke egnede for børn\n50-150 cl\nPr ltr max 19,98\nExcl pant", 9.99m),
        Current(1290, "Løvbjerg", "Rynkeby juice el. saft",
            "16 saftige æbler, søde appelsiner, eksotiske frugter el. fra haven el. rød blandet saft alm. el. light | 1 ltr el. 5 ltr drik", 16.99m),
        Current(1351, "Løvbjerg", "Innocent juice el. smoothie", "Flere varianter\n600-900 ml | Pr ltr max 36,65\nExcl pant", 21.99m),
        Current(1285, "Løvbjerg", "Bland selv slik", "Gælder KUN fredag d. 6 november", 8.99m),
        Current(1286, "Løvbjerg", "Pålækker klassiker pålæg el. leverpostej", null, 11.99m),
        Current(272, "Netto", "Røget hamburgerryg eller bacon", "PRISEN GÆLDER HELE NOVEMBER\nPr. kg 49,90\nPR. 1/2 KG", 24.95m),
        Current(1646, "Lidl", "PÅLÆGSSLAGTEREN Rullepølse, hamburgerryg eller røget rullepølse",
            "140-150 g / pk. Pr. kg maks. 99,64 Fast lav pris", 13.95m),
        Current(19, "REMA 1000", "Riberhus skiveost, Castello Gouda eller Havarti", "200-240 g\nMax. 125.00 pr. kg", 25m),
        Current(182, "REMA 1000", "Burgerboost", "Ved køb af flere end 6 stk. pr. dag er prisen 39.95 pr. stk. 2x175 g", 29m),
    ]);

    private static int[] Matches(string name, string? variant = null, string? quantity = null) =>
        [.. Today.ForItem(null, name, variant, quantity).Select(o => o.Id)];

    [Fact]
    public void B4_faxe_kondi_1_5_l_gets_the_bottle_not_the_booster()
    {
        Assert.Equal(780, Matches("Faxe Kondi", quantity: "1,5 l").First());
        Assert.DoesNotContain(730, Matches("Faxe Kondi", quantity: "1,5 l"));
    }

    [Fact]
    public void B4_a_known_wrong_size_is_dropped_when_a_package_fits()
    {
        // The planner chooses by price, so a cheaper 33 cl can must not be left as an alternative to the bottle.
        var lookup = new OfferLookup(
        [
            Current(1, "Kvickly", "Faxe Kondi eller Pepsi Max", "150 cl", 12m),
            Current(2, "Kvickly", "Faxe Kondi", "33 cl. dåse", 6m),
            Current(3, "Kvickly", "Faxe Kondi", "Flere varianter", 11m),
        ]);

        Assert.Equal([1, 3], lookup.ForItem(null, "Faxe Kondi", null, "1,5 l").Select(o => o.Id));
    }

    [Fact]
    public void B4_the_booster_still_matches_when_the_item_asks_for_it() =>
        Assert.Contains(730, Matches("Faxe Kondi Booster"));

    [Fact]
    public void B4_energy_drink_in_another_part_of_the_heading_does_not_hide_fanta() =>
        Assert.Equal([1225], Matches("Fanta", quantity: "1,5 l"));

    [Fact]
    public void B5_til_suffix_is_not_part_of_the_product()
    {
        Assert.Equal([1290, 1351], Matches("Juice til børn", quantity: "2 stk."));
        Assert.Equal([1290, 1351], Matches("Juice hvis vi mangler"));
    }

    [Fact]
    public void B6_dash_and_space_are_the_same() => Assert.Equal([1285], Matches("Bland selv-slik"));

    [Fact]
    public void B7_hamburgerryg_as_pålæg_never_matches_a_whole_smoked_one()
    {
        var matches = Matches("Hamburgerryg", "pålæg", "1 pk.");

        Assert.DoesNotContain(272, matches);
        Assert.DoesNotContain(1286, matches); // acceptable miss (low priority)
    }

    // ---- C. The trip -------------------------------------------------------------------------------------------

    private static readonly PlanStore Rema = new(1, "REMA 1000", true, null);
    private static readonly PlanStore Kvickly = new(3, "Kvickly", false, null);
    private static readonly PlanStore Lovbjerg = new(4, "Løvbjerg", false, null);

    [Fact]
    public void C8_home_store_comes_first()
    {
        List<PlanItem> items =
        [
            new(1, "Gavepapir", null, "1 rulle", null, Kvickly.Id, false, [new PlanOffer(830, Kvickly.Id, "Gavepapir", 10m, null, false, Friday)]),
            new(2, "Mælk", null, "1 l", null, Lovbjerg.Id, false, [new PlanOffer(1214, Lovbjerg.Id, "Arla mælk", 9.99m, null, false, Friday)]),
            new(3, "Æg", null, "2 bakker", null, null, false, [new PlanOffer(8, Rema.Id, "Skrabeæg", 18m, null, false, Friday)]),
        ];

        var plan = StoreSelector.Choose([Rema, Kvickly, Lovbjerg], items, 50m);

        Assert.Equal("REMA 1000", plan.Visits[0].Store.Name);
    }

    [Fact]
    public void C8_saved_trip_view_lists_the_home_store_first()
    {
        var ordered = TripViews.OrderVisits(
        [
            new(3, "Kvickly", "#c8102e", false, 32m, []),
            new(4, "Løvbjerg", "#e2001a", false, 20m, []),
            new(1, "REMA 1000", "#0a2d6e", true, 154m, []),
        ]);

        Assert.Equal(["REMA 1000", "Kvickly", "Løvbjerg"], ordered.Select(v => v.Store));
    }

    private static readonly PlanOffer Cheese19 = new(19, Rema.Id, "Riberhus skiveost, Castello Gouda eller Havarti", 25m, null, false, Friday);

    [Fact]
    public void C9_generic_item_on_the_same_offer_as_a_specific_one_saves_once()
    {
        List<PlanItem> items =
        [
            new(41, "Havarti", "Castello", "1 stk.", null, Rema.Id, false, [Cheese19]),
            new(42, "Ost", null, "1 stk.", null, Rema.Id, false, [Cheese19]),
        ];

        var plan = StoreSelector.Choose([Rema], items, 50m);

        Assert.Equal(6.25m, plan.EstimatedSaving); // 25 % of 25 kr., once
    }

    [Fact]
    public void C9_generic_item_takes_another_offer_when_there_is_one()
    {
        var danbo = new PlanOffer(99, Rema.Id, "Danbo ost 45+", 30m, null, false, Friday);
        List<PlanItem> items =
        [
            new(41, "Havarti", "Castello", "1 stk.", null, Rema.Id, false, [Cheese19]),
            new(42, "Ost", null, "1 stk.", null, Rema.Id, false, [Cheese19, danbo]),
        ];

        var plan = StoreSelector.Choose([Rema], items, 50m);

        var ost = plan.Visits.Single().Items.Single(i => i.Item.Name == "Ost");
        Assert.Equal(99, ost.Offer!.Id);
    }

    [Fact]
    public void C9_ost_is_not_the_end_of_burgerboost() => Assert.DoesNotContain(182, Matches("Ost"));

    // ---- 10. One product per item ------------------------------------------------------------------------------

    [Fact]
    public void P10_tool_descriptions_ask_for_one_product_per_item()
    {
        var add = typeof(ShoppingListTools).GetMethod(nameof(ShoppingListTools.AddToShoppingListAsync))!
            .GetCustomAttribute<DescriptionAttribute>()!.Description;
        var extra = typeof(ShoppingListTools).GetMethod(nameof(ShoppingListTools.BuildShoppingListAsync))!
            .GetParameters().Single(p => p.Name == "extraItems").GetCustomAttribute<DescriptionAttribute>()!.Description;

        foreach (var text in new[] { add, extra })
        {
            Assert.Contains("One item per product", text);
            Assert.Contains("'Natron, bagepulver og vaniljesukker' must be three items", text);
            Assert.Contains("never a note", text);
        }
    }

    [Fact]
    public void P10_warns_about_combined_names_without_an_ingredient()
    {
        var warnings = AddItems.Warnings(
        [
            new ShoppingListItem { Name = "Natron, bagepulver og vaniljesukker" },
            new ShoppingListItem { Name = "Salt og peber", IngredientId = 3 }, // a known ingredient: fine
            new ShoppingListItem { Name = "Smør" },
        ]);

        var warning = Assert.Single(warnings);
        Assert.Contains("Natron, bagepulver og vaniljesukker", warning);
    }

    // ---- 11. The trip by department ----------------------------------------------------------------------------

    [Fact]
    public void D11_trip_text_lists_each_store_by_department_in_walking_order()
    {
        var rema = new PlanStore(1, "REMA 1000", true, null);
        PlanItem Item(int id, string name, string category, int sort, string? tag) =>
            new(id, name, null, null, tag, null, false, [], category, sort);
        var plan = new StoreSelector.Plan(
        [
            new StoreSelector.Visit(rema,
            [
                new(Item(1, "Hvedemel", "Kolonial", 6, "Bagning"), null, 1, null),
                new(Item(2, "Citroner", "Frugt og grønt", 1, "Bagning"), null, 1, null),
                new(Item(3, "Natbleer", "Personlig pleje", 11, null), null, 1, null),
                new(Item(4, "Ost", "Mejeri", 5, "Fødselsdag"), null, 1, null),
                new(Item(5, "Æg", "Mejeri", 5, "Bagning"), null, 1, null),
            ]),
        ], 0);

        var lines = TripText.Format(plan, Texts()).ReplaceLineEndings("\n").Split('\n');

        Assert.Equal("REMA 1000", lines[0]);
        Assert.Equal(["Frugt og grønt", "Mejeri", "Kolonial", "Personlig pleje"],
            lines.Where(l => !l.StartsWith("- ") && l != "REMA 1000"));
        Assert.Equal(["Citroner", "Ost", "Æg", "Hvedemel", "Natbleer"],
            lines.Where(l => l.StartsWith("- ")).Select(l => l[6..].Split([' ', ','])[0]));
        Assert.Contains(lines, l => l.StartsWith("- [ ] Æg") && l.Contains("Bagning"));
    }

    [Fact]
    public void D11_trip_items_carry_their_category()
    {
        var mejeri = new Category { Id = 5, Name = "Mejeri", Color = "#7aa9d9", SortOrder = 5 };
        var item = new ShoppingTripItem
        {
            Id = 1, Name = "Æg", ShoppingListItem = new ShoppingListItem { Id = 38, Name = "Æg", CategoryId = 5 },
        };

        var dto = TripViews.ToItem(item, active: true, new Dictionary<int, Category> { [5] = mejeri })!;

        Assert.Equal("Mejeri", dto.Category);
        Assert.Equal("#7aa9d9", dto.CategoryColor);
        Assert.Equal(5, dto.CategorySort);
    }

    private static IStringLocalizer<TripTexts> Texts()
    {
        var localizer = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance)
            .Create(typeof(TripTexts));
        return new StringLocalizer<TripTexts>(new FactoryWrapper(localizer));
    }

    private sealed class FactoryWrapper(IStringLocalizer localizer) : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource) => localizer;
        public IStringLocalizer Create(string baseName, string location) => localizer;
    }
}
