using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Offers;

namespace MealPlanner.Tests.Common;

public class OfferLookupTests
{
    private static OfferLookup.CurrentOffer Offer(int id, string heading, string? description = null) =>
        new(id, 1, "REMA 1000", heading, description, 10m, null, false, false, new DateOnly(2026, 11, 7), []);

    private static readonly OfferLookup Lookup = new(
    [
        Offer(1, "HATTING Kanelsnegle, tebirkes eller møllehjul"),
        Offer(2, "Santa Maria kanel 30 g"),
        Offer(3, "Faxe Kondi eller Pepsi 1,5 l"),
        Offer(4, "Arla rygeost"),
        Offer(5, "Lurpak smør", "Gælder også smørbar"),
        Offer(6, "Castello havarti"),
        Offer(7, "Danbo ost 45+"),
    ]);

    private static int[] Ids(string name, string? variant = null) =>
        [.. Lookup.ForItem(null, name, variant).Select(o => o.Id).Order()];

    [Fact]
    public void Name_must_not_be_the_first_part_of_a_compound() => Assert.Equal([2], Ids("Kanel"));

    [Fact]
    public void Multi_word_names_match_whole_words() => Assert.Equal([3], Ids("Faxe Kondi"));

    [Fact]
    public void Name_may_be_the_last_part_of_a_compound() => Assert.Equal([4, 7], Ids("ost"));

    [Fact]
    public void Variant_narrows_to_the_brand() => Assert.Equal([6], Ids("havarti", "Castello"));

    [Fact]
    public void Variant_after_ikke_excludes() => Assert.Empty(Ids("smør", "rigtigt smør, ikke smørbar"));

    [Theory]
    [InlineData("rigtigt smør, ikke smørbar", "smør", "smørbar")]
    [InlineData("Castello", "castello", "")]
    public void Parses_variant(string variant, string required, string excluded)
    {
        var (req, exc) = OfferLookup.ParseVariant(variant);
        Assert.Equal(required, string.Join(",", req));
        Assert.Equal(excluded, string.Join(",", exc));
    }

    [Fact]
    public void Normalized_heading_is_used() =>
        Assert.True(OfferLookup.NameAppearsIn("kanel", IngredientMatcher.Normalize("Kanel* 30 g")));
}
