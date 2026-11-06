using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Data;

/// <summary>
/// Starter data. Category, store and ingredient names are Danish because they are user-facing data, not code.
/// Each block runs only when its table is empty; everything here can be edited afterwards.
/// Runs from EF's seeding hook, i.e. on every migration at startup.
/// </summary>
public static class SeedData
{
    // Sort order follows the walk through the home store (REMA 1000).
    private static readonly (string Name, string Color)[] Categories =
    [
        ("Frugt og grønt", "#5a9a3f"), ("Brød", "#d9921e"), ("Kød, fisk og fjerkræ", "#e04b3c"),
        ("Pålæg", "#a8653f"), ("Mejeri", "#7aa9d9"), ("Kolonial", "#7d685c"), ("Frost", "#5bb5c9"),
        ("Drikkevarer", "#3f6fb5"), ("Slik og snacks", "#c75b9b"), ("Husholdning", "#4aa3a3"),
        ("Personlig pleje", "#9a7fc2"), ("Andet", "#8f6f99"),
    ];

    // Tjek dealer ids are verified against live data (spec 12.2). Colors are approximate brand colors.
    private static readonly (string Name, string TjekId, string Color)[] Stores =
    [
        ("REMA 1000", "11deC", "#0a2d6e"), ("Netto", "9ba51", "#ffd200"), ("Kvickly", "c1edq", "#c8102e"),
        ("Løvbjerg", "65caN", "#e2001a"), ("Lidl", "71c90", "#0050aa"), ("Coop 365", "DWZE1w", "#00a0e1"),
    ];

    private const string HomeStore = "REMA 1000";

    // Lidl and Coop 365 in Odder are next to each other and count as one stop.
    private static readonly Dictionary<string, string> StopGroups = new()
    {
        ["Lidl"] = "Lidl og Coop 365",
        ["Coop 365"] = "Lidl og Coop 365",
    };

    // Ingredient, category, keywords, exclusions.
    private static readonly (string Name, string Category, string[] Keywords, string[] Exclusions)[] Ingredients =
    [
        ("Hakket oksekød", "Kød, fisk og fjerkræ", ["hakket oksekød", "hakket dansk oksekød", "hakket boost", "burger boost"], []),
        ("Hakket grisekød", "Kød, fisk og fjerkræ", ["hakket grisekød", "hakket dansk grisekød", "hakket gris"], ["kalv", "okse"]),
        ("Hakket gris & kalv", "Kød, fisk og fjerkræ", ["gris & kalv", "grise & kalv", "gris og kalv", "grise- og kalv"], []),
        ("Hakket kylling", "Kød, fisk og fjerkræ", ["hakket kylling", "hakket dansk kylling", "hakket kyllingekød"], []),
        ("Hel kylling", "Kød, fisk og fjerkræ", ["hel kylling", "hel dansk kylling", "dansk hel kylling"], []),
        ("Kyllingebryst", "Kød, fisk og fjerkræ", ["kyllingebryst", "kyllingebrystfilet", "inderfilet"], ["pålæg"]),
        ("Kyllingelår", "Kød, fisk og fjerkræ", ["kyllingelår", "kyllingelårfilet"], []),
        ("Svinemørbrad", "Kød, fisk og fjerkræ", ["mørbrad af gris", "dansk mørbrad", "danske mørbrad", "svinemørbrad"], []),
        ("Flæskesteg", "Kød, fisk og fjerkræ", ["flæskesteg"], ["svær"]),
        ("Ribbensteg", "Kød, fisk og fjerkræ", ["ribbensteg", "ribbenssteg"], []),
        ("Nakkefilet", "Kød, fisk og fjerkræ", ["nakkefilet", "grisekamfilet"], []),
        ("Koteletter", "Kød, fisk og fjerkræ", ["kotelet", "koteletter"], []),
        ("Flæsk i skiver", "Kød, fisk og fjerkræ", ["flæsk i skiver"], []),
        ("Spareribs", "Kød, fisk og fjerkræ", ["spareribs"], []),
        ("Pølser", "Kød, fisk og fjerkræ", ["pølser", "medister", "medisterpølse"], ["spegepølse", "cocktail", "brunch"]),
        ("Bacon", "Kød, fisk og fjerkræ", ["bacon"], ["kalkunbacon", "chips"]),
        ("Oksebøffer", "Kød, fisk og fjerkræ", ["tykstegsbøf", "tykstegbøf", "oksefilet", "oksemedaljon", "entrecote", "ribeye", "culotte"], []),
        ("Oksekød i tern", "Kød, fisk og fjerkræ", ["oksekød i tern", "gullaschkød"], []),
        ("Laks", "Kød, fisk og fjerkræ", ["laksefilet", "laksestykke", "lakseside", "laks"], ["røget", "varmrøget"]),
        ("Hvid fisk", "Kød, fisk og fjerkræ", ["rødspætte", "torsk", "sej", "kuller"], []),
        ("Rejer", "Kød, fisk og fjerkræ", ["rejer"], []),
        ("Fiskefrikadeller", "Kød, fisk og fjerkræ", ["fiskefrikadeller"], []),
        ("And", "Kød, fisk og fjerkræ", ["andebryst", "andelår", "hel and", "landand", "berberiand"], ["andefedt", "menu"]),
        ("Kartofler", "Frugt og grønt", ["kartofler", "bagekartofler"], ["fløde", "brunede", "chips", "franske", "glas", "gratin"]),
        ("Flødekartofler", "Frost", ["flødekartofler", "kartoffelgratin"], []),
        ("Løg", "Frugt og grønt", ["løg", "rødløg"], []),
        ("Gulerødder", "Frugt og grønt", ["gulerødder", "gulerod"], []),
        ("Porrer", "Frugt og grønt", ["porrer", "porre"], []),
        ("Broccoli", "Frugt og grønt", ["broccoli"], []),
        ("Peberfrugt", "Frugt og grønt", ["peberfrugt"], []),
        ("Agurk", "Frugt og grønt", ["agurk"], []),
        ("Asparges", "Kolonial", ["asparges"], []),
        ("Rødkål", "Kolonial", ["rødkål"], []),
        ("Hvidkål", "Frugt og grønt", ["hvidkål"], []),
        ("Græskar", "Frugt og grønt", ["græskar", "hokkaido"], ["frø", "brød"]),
        ("Bananer", "Frugt og grønt", ["bananer", "banan"], []),
        ("Pasta", "Kolonial", ["pasta", "spaghetti", "penne", "lasagneplader"], ["sauce"]),
        ("Pastasauce", "Kolonial", ["pastasauce", "dolmio"], []),
        ("Ris", "Kolonial", ["ris", "jasminris", "basmatiris"], ["ris a la mande", "risalamande"]),
        ("Tortilla", "Kolonial", ["tortilla", "wraps"], []),
        ("Tarteletter", "Brød", ["tarteletter"], []),
        ("Fløde", "Mejeri", ["piskefløde", "madlavningsfløde", "fløde"], ["flødeis", "flødeboller", "flødeost", "flødekartofler"]),
        ("Æg", "Mejeri", ["æg", "skrabeæg", "frilandsæg"], []),
        ("Mælk", "Mejeri", ["mælk", "letmælk", "minimælk", "sødmælk", "skummetmælk"], ["kakao", "chokolade", "kokos"]),
        ("Kakaomælk", "Mejeri", ["kakaomælk", "kakao", "chokolademælk", "cocio", "matilde"], []),
        ("Hytteost", "Mejeri", ["hytteost"], []),
        ("Toiletpapir", "Husholdning", ["toiletpapir"], []),
    ];

    // Starter dictionary for automatic categorisation of shopping items (on top of the ingredients above).
    private static readonly (string Category, string[] Terms)[] CategoryTerms =
    [
        ("Frugt og grønt", ["æble", "pære", "appelsin", "citron", "lime", "vindruer", "bær", "blåbær", "hindbær", "jordbær", "melon", "kiwi", "avocado", "tomat", "cherrytomater", "salat", "feldsalat", "spinat", "squash", "champignon", "svampe", "blomkål", "selleri", "pastinak", "rødbede", "hvidløg", "ingefær", "persille", "basilikum", "purløg", "majs", "ærter", "bønner", "sødkartofler", "mango", "ananas", "klementiner", "mandariner"]),
        ("Brød", ["brød", "rugbrød", "franskbrød", "boller", "rundstykker", "bagels", "toastbrød", "pitabrød", "flutes", "croissant", "knækbrød", "kiks", "burgerboller", "pølsebrød", "kagebund", "lagkagebunde"]),
        ("Kød, fisk og fjerkræ", ["kød", "kylling", "frikadeller", "fars", "hakket", "steg", "filet", "fisk", "tun", "makrel", "kalkun", "lam", "vildt"]),
        ("Pålæg", ["pålæg", "kyllingepålæg", "skinke", "hamburgerryg", "rullepølse", "spegepølse", "salami", "leverpostej", "postej", "hønsesalat", "tunsalat", "makrel i tomat", "pålægschokolade", "smørepålæg"]),
        ("Mejeri", ["smør", "smørbar", "yoghurt", "skyr", "ymer", "a38", "creme fraiche", "ost", "skæreost", "revet ost", "mozzarella", "feta", "parmesan", "flødeost", "smøreost", "kærnemælk", "piskefløde", "madlavningsfløde"]),
        ("Kolonial", ["mel", "hvedemel", "sukker", "flormelis", "salt", "peber", "krydderi", "olie", "olivenolie", "eddike", "ketchup", "remoulade", "mayonnaise", "sennep", "bouillon", "hakkede tomater", "tomatpure", "kokosmælk", "havregryn", "müsli", "cornflakes", "honning", "marmelade", "nutella", "nudler", "linser", "kikærter", "frugtstænger", "rosiner", "nødder", "gær", "bagepulver", "vaniljesukker", "kakao pulver", "konserves", "dåsetomater"]),
        ("Frost", ["is", "frosne", "frost", "pommes frites", "frosne ærter", "pizza", "fiskefilet frost", "kyllingenuggets"]),
        ("Drikkevarer", ["sodavand", "juice", "saft", "vand", "danskvand", "kaffe", "te", "øl", "vin", "cola", "squash drik", "energidrik", "kakaomælk"]),
        ("Slik og snacks", ["slik", "chips", "chokolade", "popcorn", "kage", "småkager", "vingummi", "lakrids", "saltstænger", "flødeboller"]),
        ("Husholdning", ["køkkenrulle", "køkkenruller", "opvaskemiddel", "opvasketabs", "vaskepulver", "vaskemiddel", "skyllemiddel", "affaldsposer", "skraldeposer", "fryseposer", "bagepapir", "sølvpapir", "film", "madopbevaring", "stearinlys", "batterier", "rengøring", "klude", "svampe til opvask", "toiletrens"]),
        ("Personlig pleje", ["tandpasta", "tandbørste", "shampoo", "balsam", "sæbe", "håndsæbe", "deodorant", "bleer", "vatrondeller", "plaster", "solcreme", "barberskrabere", "hårelastikker"]),
    ];

    // The v1 household (spec 2 and 12.1): Odder, 20 km radius, third store only when it saves more than 50 kr.
    private static readonly Household DefaultHousehold = new()
    {
        Name = "Marc og Pia",
        Latitude = 55.9727,
        Longitude = 10.1526,
        RadiusMeters = 20_000,
        ThirdStoreThreshold = 50m,
    };

    public static void Seed(DbContext context) => SeedAsync(context).GetAwaiter().GetResult();

    public static async Task SeedAsync(DbContext context, CancellationToken ct = default)
    {
        var db = (AppDbContext)context;
        await SeedReferenceDataAsync(db, ct);
        await SeedHouseholdAsync(db, ct);
    }

    private static async Task SeedReferenceDataAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;

        var categories = Categories.Select((c, i) => new Category { Name = c.Name, Color = c.Color, SortOrder = i + 1 }).ToList();
        db.Categories.AddRange(categories);
        var byName = categories.ToDictionary(c => c.Name);

        db.Stores.AddRange(Stores.Select(s => new Store { Name = s.Name, TjekDealerId = s.TjekId, Color = s.Color }));

        foreach (var (name, category, keywords, exclusions) in Ingredients)
        {
            db.Ingredients.Add(new Ingredient
            {
                Name = name,
                Category = byName[category],
                Aliases =
                [
                    .. keywords.Select(k => new IngredientAlias { Text = k }),
                    .. exclusions.Select(x => new IngredientAlias { Text = x, IsExclusion = true }),
                ],
            });
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (category, terms) in CategoryTerms)
            foreach (var term in terms.Where(seen.Add))
                db.CategoryRules.Add(new CategoryRule { Term = term, Category = byName[category] });

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedHouseholdAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Households.AnyAsync(ct)) return;

        var stores = await db.Stores.ToListAsync(ct);
        var household = new Household
        {
            Name = DefaultHousehold.Name,
            Latitude = DefaultHousehold.Latitude,
            Longitude = DefaultHousehold.Longitude,
            RadiusMeters = DefaultHousehold.RadiusMeters,
            ThirdStoreThreshold = DefaultHousehold.ThirdStoreThreshold,
            Stores =
            [
                .. stores.Select(s => new HouseholdStore
                {
                    Store = s,
                    IsHomeStore = s.Name == HomeStore,
                    StopGroup = StopGroups.GetValueOrDefault(s.Name),
                }),
            ],
        };
        db.Households.Add(household);
        await db.SaveChangesAsync(ct);
    }
}
