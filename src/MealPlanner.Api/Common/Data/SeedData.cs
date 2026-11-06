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
    internal static readonly (string Name, string Color)[] Categories =
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

    // Ingredient, category, keywords, exclusions. Internal so matcher tests run against the real seed.
    internal static readonly (string Name, string Category, string[] Keywords, string[] Exclusions)[] Ingredients =
    [
        ("Hakket oksekød", "Kød, fisk og fjerkræ", ["hakket oksekød", "hakket dansk oksekød", "hakket boost", "burger boost"], []),
        ("Hakket grisekød", "Kød, fisk og fjerkræ", ["hakket grisekød", "hakket dansk grisekød", "hakket gris"], ["kalv", "okse"]),
        ("Hakket gris & kalv", "Kød, fisk og fjerkræ", ["gris & kalv", "grise & kalv", "gris og kalv", "grise- og kalv"], []),
        ("Hakket kylling", "Kød, fisk og fjerkræ", ["hakket kylling", "hakket dansk kylling", "hakket kyllingekød"], []),
        ("Hel kylling", "Kød, fisk og fjerkræ", ["hel kylling", "hel dansk kylling", "dansk hel kylling"], []),
        ("Kyllingebryst", "Kød, fisk og fjerkræ", ["kyllingebryst", "kyllingebrystfilet", "inderfilet"], ["pålæg"]),
        ("Kyllingelår", "Kød, fisk og fjerkræ", ["kyllingelår", "kyllingelårfilet"], []),
        ("Svinemørbrad", "Kød, fisk og fjerkræ", ["mørbrad af gris", "dansk mørbrad", "danske mørbrad", "svinemørbrad"], []),
        ("Flæskesteg", "Kød, fisk og fjerkræ", ["flæskesteg"], ["svær", "sandwich", "sauce"]),
        ("Ribbensteg", "Kød, fisk og fjerkræ", ["ribbensteg", "ribbenssteg"], []),
        ("Nakkefilet", "Kød, fisk og fjerkræ", ["nakkefilet", "grisekamfilet"], []),
        ("Koteletter", "Kød, fisk og fjerkræ", ["kotelet", "koteletter"], []),
        ("Flæsk i skiver", "Kød, fisk og fjerkræ", ["flæsk i skiver"], []),
        ("Spareribs", "Kød, fisk og fjerkræ", ["spareribs"], []),
        ("Pølser", "Kød, fisk og fjerkræ", ["pølser", "medister", "medisterpølse"], ["spegepølse", "cocktail", "brunch"]),
        ("Bacon", "Kød, fisk og fjerkræ", ["bacon"], ["kalkunbacon", "chips", "postej"]),
        ("Oksebøffer", "Kød, fisk og fjerkræ", ["tykstegsbøf", "tykstegbøf", "oksefilet", "oksemedaljon", "entrecote", "ribeye", "culotte"], []),
        ("Oksekød i tern", "Kød, fisk og fjerkræ", ["oksekød i tern", "gullaschkød"], []),
        ("Laks", "Kød, fisk og fjerkræ", ["laksefilet", "laksestykke", "lakseside", "laks"], ["røget", "varmrøget"]),
        ("Hvid fisk", "Kød, fisk og fjerkræ", ["rødspætte", "torsk", "sej", "kuller"], []),
        ("Rejer", "Kød, fisk og fjerkræ", ["rejer"], []),
        ("Fiskefrikadeller", "Kød, fisk og fjerkræ", ["fiskefrikadeller"], []),
        ("And", "Kød, fisk og fjerkræ", ["andebryst", "andelår", "hel and", "landand", "berberiand"], ["andefedt", "menu", "sauce"]),
        ("Kartofler", "Frugt og grønt", ["kartofler", "bagekartofler"], ["fløde", "brunede", "chips", "franske", "glas", "gratin", "søde"]),
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
        ("Bananer", "Frugt og grønt", ["bananer", "banan"], ["skum"]),
        ("Pasta", "Kolonial", ["pasta", "spaghetti", "penne", "lasagneplader"], ["sauce", "tandpasta", "vaniljepasta"]),
        ("Pastasauce", "Kolonial", ["pastasauce", "dolmio"], []),
        ("Ris", "Kolonial", ["ris", "jasminris", "basmatiris"], ["ris a la mande", "risalamande"]),
        ("Tortilla", "Kolonial", ["tortilla", "wraps"], []),
        ("Tarteletter", "Brød", ["tarteletter"], []),
        ("Fløde", "Mejeri", ["piskefløde", "madlavningsfløde", "fløde"], ["flødeis", "flødeboller", "flødeost", "flødekartofler", "havarti", "jævner"]),
        ("Æg", "Mejeri", ["æg", "skrabeæg", "frilandsæg"], ["spejle"]),
        ("Mælk", "Mejeri", ["mælk", "letmælk", "minimælk", "sødmælk", "skummetmælk"], ["kakao", "chokolade", "kokos"]),
        // Brand words (spec 12.4) instead of plain "kakao", which would hit baking cocoa and "kakaocreme".
        ("Kakaomælk", "Mejeri", ["kakaomælk", "chokolademælk", "cocio", "matilde", "blue keld"], []),
        ("Hytteost", "Mejeri", ["hytteost"], []),
        ("Toiletpapir", "Husholdning", ["toiletpapir"], ["holder"]),
    ];

    // Starter dictionary for automatic categorisation of shopping items (on top of the ingredients above).
    // Words are matched whole or as the end of a compound ("rugbrød" -> "brød"), so heads of compounds go here.
    // Internal so classifier tests run against the real dictionary.
    internal static readonly (string Category, string[] Terms)[] CategoryTerms =
    [
        ("Frugt og grønt", ["frugt", "grønt", "grøntsager", "æble", "æbler", "pære", "pærer", "appelsin", "appelsiner", "citron", "citroner", "lime", "limefrugter", "vindruer", "blommer", "nektariner", "ferskner", "abrikoser", "granatæble", "grapefrugt", "dadler", "figner", "bær", "blåbær", "hindbær", "jordbær", "solbær", "ribs", "brombær", "tranebær", "melon", "vandmelon", "kiwi", "avocado", "mango", "ananas", "passionsfrugt", "klementiner", "mandariner", "rabarber", "tomat", "tomater", "cherrytomater", "salat", "feldsalat", "rucola", "icebergsalat", "hjertesalat", "spinat", "squash", "aubergine", "champignon", "champignoner", "svampe", "kantareller", "østershatte", "blomkål", "rosenkål", "grønkål", "spidskål", "kinakål", "pak choi", "selleri", "bladselleri", "knoldselleri", "fennikel", "pastinak", "persillerod", "rødbede", "rødbeder", "radiser", "jordskokker", "hvidløg", "skalotteløg", "forårsløg", "ingefær", "chili", "persille", "basilikum", "purløg", "dild", "koriander", "mynte", "timian", "rosmarin", "majs", "majskolber", "ærter", "sukkerærter", "bønner", "bønnespirer", "sødkartofler", "kokosnød"]),
        ("Brød", ["brød", "rugbrød", "franskbrød", "surdejsbrød", "fuldkornsbrød", "sandwichbrød", "toast", "toastbrød", "ciabatta", "baguette", "flutes", "boller", "grovboller", "teboller", "rundstykker", "bagels", "pitabrød", "naanbrød", "fladbrød", "croissant", "kanelsnegle", "wienerbrød", "tebirkes", "knækbrød", "kiks", "burgerboller", "pølsebrød", "hotdogbrød", "kagebund", "lagkagebunde", "pandekager", "scones"]),
        ("Kød, fisk og fjerkræ", ["kød", "oksekød", "grisekød", "svinekød", "kalvekød", "lammekød", "kylling", "kyllingevinger", "kyllingeunderlår", "kalkunbryst", "frikadeller", "frikadellefars", "fars", "hakket", "steg", "tyksteg", "culottesteg", "filet", "bøf", "bøffer", "schnitzler", "kødboller", "pulled pork", "fisk", "fiskefilet", "torsk", "rødspætte", "sild", "tun", "makrel", "muslinger", "kammuslinger", "krabbe", "rogn", "stenbiderrogn", "kalkun", "lam", "vildt"]),
        ("Pålæg", ["pålæg", "kyllingepålæg", "skinke", "hamburgerryg", "rullepølse", "spegepølse", "kødpølse", "salami", "pepperoni", "chorizo", "serranoskinke", "parmaskinke", "roastbeef", "pastrami", "leverpostej", "postej", "hønsesalat", "tunsalat", "rejesalat", "æggesalat", "karrysalat", "italiensk salat", "makrel i tomat", "pålægschokolade", "smørepålæg"]),
        ("Mejeri", ["smør", "smørbar", "plantesmør", "lurpak", "milbona", "kærgården", "margarine", "yoghurt", "græsk yoghurt", "skyr", "ymer", "a38", "tykmælk", "kefir", "creme fraiche", "cremefraiche", "ost", "skæreost", "revet ost", "mozzarella", "feta", "parmesan", "cheddar", "brie", "camembert", "danbo", "havarti", "castello", "gouda", "rygeost", "mascarpone", "ricotta", "halloumi", "flødeost", "smøreost", "kærnemælk", "piskefløde", "madlavningsfløde", "havredrik", "havremælk", "sojadrik", "mandeldrik", "koldskål", "budding", "risalamande"]),
        ("Kolonial", ["mel", "hvedemel", "rugmel", "speltmel", "grahamsmel", "majsstivelse", "kartoffelmel", "sukker", "rørsukker", "farin", "flormelis", "sirup", "salt", "peber", "krydderi", "kanel", "kardemomme", "nelliker", "muskatnød", "karry", "paprika", "spidskommen", "oregano", "laurbærblade", "vaniljestang", "kakao", "olie", "rapsolie", "olivenolie", "eddike", "balsamico", "ketchup", "remoulade", "mayonnaise", "sennep", "dressing", "bouillon", "fond", "sovsekulør", "tomatsauce", "pesto", "soja", "sojasauce", "fiskesauce", "østerssauce", "sweet chili", "hakkede tomater", "flåede tomater", "passata", "tomatpure", "kokosmælk", "kidneybønner", "sorte bønner", "linser", "kikærter", "bulgur", "couscous", "quinoa", "perlespelt", "nudler", "risnudler", "makaroni", "havregryn", "müsli", "granola", "cornflakes", "honning", "marmelade", "syltetøj", "nutella", "peanutbutter", "jordnøddesmør", "frugtstænger", "rosiner", "nødder", "mandler", "hasselnødder", "valnødder", "cashewnødder", "solsikkekerner", "græskarkerner", "chiafrø", "hørfrø", "kokosmel", "marcipan", "overtræk", "blå birkes", "birkes", "gær", "tørgær", "bagepulver", "natron", "vaniljesukker", "kakao pulver", "syltede agurker", "asier", "kapers", "oliven", "soltørrede tomater", "tacokrydderi", "tacoskaller", "salsa", "konserves", "dåsetomater", "babymad", "grød"]),
        ("Frost", ["is", "flødeis", "sorbet", "isvafler", "frosne", "frossen", "frost", "pommes frites", "kartoffelbåde", "frosne ærter", "frosne bær", "wokgrøntsager", "pizza", "fiskepinde", "fiskefilet frost", "kyllingenuggets", "forårsruller", "færdigretter", "butterdej", "æbleskiver", "isterninger"]),
        ("Drikkevarer", ["sodavand", "juice", "appelsinjuice", "æblejuice", "smoothie", "saft", "saftevand", "vand", "danskvand", "kildevand", "mineralvand", "cola", "pepsi", "fanta", "faxe kondi", "sprite", "tonic", "iste", "capri-sun", "kaffe", "kaffebønner", "kaffekapsler", "te", "øl", "maltøl", "cider", "vin", "rødvin", "hvidvin", "rosévin", "cava", "champagne", "gløgg", "spiritus", "snaps", "gin", "vodka", "rom", "whisky", "likør", "squash drik", "energidrik", "kakaomælk", "chokolademælk"]),
        ("Slik og snacks", ["slik", "bland selv slik", "chips", "tortillachips", "chokolade", "chokoladebar", "popcorn", "kage", "småkager", "vafler", "vingummi", "lakrids", "karamel", "karameller", "tyggegummi", "pastiller", "slikkepinde", "konfekt", "pebernødder", "saltstænger", "peanuts", "snacknødder", "dip", "flødeboller"]),
        ("Husholdning", ["køkkenrulle", "køkkenruller", "servietter", "opvaskemiddel", "opvasketabs", "maskinopvask", "vaskepulver", "vaskemiddel", "skyllemiddel", "pletfjerner", "affaldsposer", "skraldeposer", "fryseposer", "frysebøtter", "bagepapir", "sølvpapir", "alufolie", "plastfolie", "film", "madopbevaring", "stearinlys", "fyrfadslys", "tændstikker", "elpærer", "batterier", "rengøring", "rengøringsmiddel", "glasrens", "kalkfjerner", "afkalker", "toiletrens", "wc rens", "klude", "karklude", "opvaskebørste", "svampe til opvask", "skuresvampe", "engangshandsker"]),
        ("Personlig pleje", ["tandpasta", "tandbørste", "tandtråd", "mundskyl", "shampoo", "balsam", "hårvoks", "hårspray", "sæbe", "håndsæbe", "showergel", "deo", "deodorant", "bodylotion", "håndcreme", "ansigtscreme", "læbepomade", "barberskum", "barberblade", "barberskrabere", "bleer", "natbleer", "vådservietter", "pusleservietter", "bind", "tamponer", "trusseindlæg", "vatrondeller", "vatpinde", "plaster", "solcreme", "hovedpinepiller", "vitaminer", "hårelastikker"]),
        ("Andet", ["gavekort", "blomster", "buket", "frimærker", "kattemad", "hundemad", "kattegrus", "dyrefoder", "indkøbsposer"]),
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
        await SeedMissingCategoryRulesAsync(db, ct);
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

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The dictionary grows between versions, so missing shared words are added on every start.
    /// Existing rules (also edited ones) are never touched.
    /// </summary>
    private static async Task SeedMissingCategoryRulesAsync(AppDbContext db, CancellationToken ct)
    {
        var byName = await db.Categories.ToDictionaryAsync(c => c.Name, ct);
        var existing = (await db.CategoryRules.Where(r => r.HouseholdId == null).Select(r => r.Term).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (category, terms) in CategoryTerms)
            foreach (var term in terms.Where(existing.Add))
                db.CategoryRules.Add(new CategoryRule { Term = term, CategoryId = byName[category].Id });

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
