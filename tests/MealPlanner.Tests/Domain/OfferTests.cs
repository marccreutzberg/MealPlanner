using MealPlanner.Api.Domain;

namespace MealPlanner.Tests.Domain;

public class OfferTests
{
    private static Offer Create(string heading, string? description = null,
        string from = "2026-10-30T23:00:00+00:00", string to = "2026-11-06T22:59:59+00:00")
    {
        var offer = new Offer();
        offer.Update(heading, description, 10m, null, DateTimeOffset.Parse(from), DateTimeOffset.Parse(to));
        return offer;
    }

    [Fact]
    public void Validity_uses_danish_dates_for_tjek_end_of_day()
    {
        // Netto, Saturday 31/10 to Friday 6/11 as delivered by Tjek.
        var offer = Create("Æg");

        Assert.Equal(new DateOnly(2026, 10, 31), offer.FirstValidDay);
        Assert.Equal(new DateOnly(2026, 11, 6), offer.LastValidDay);
        Assert.True(offer.IsValidOn(new DateOnly(2026, 11, 6)));
        Assert.False(offer.IsValidOn(new DateOnly(2026, 11, 7)));
        Assert.False(offer.IsValidOn(new DateOnly(2026, 10, 30)));
    }

    [Fact]
    public void Validity_treats_local_midnight_end_as_the_day_before()
    {
        var offer = Create("Æg", to: "2026-11-06T23:00:00+00:00");

        Assert.Equal(new DateOnly(2026, 11, 6), offer.LastValidDay);
    }

    [Fact]
    public void Validity_handles_summer_time_start()
    {
        // Lidl "fast lav pris" started 3/5 (CEST, UTC+2).
        var offer = Create("Piskefløde", from: "2026-05-02T22:00:00+00:00", to: "2026-12-31T22:59:59+00:00");

        Assert.Equal(new DateOnly(2026, 5, 3), offer.FirstValidDay);
        Assert.Equal(new DateOnly(2026, 12, 31), offer.LastValidDay);
    }

    [Theory]
    [InlineData("MADVÆRKET Piskefløde", "Fast lav pris / 36 % fedt. 500 ml.", true)]
    [InlineData("Dansk mørbrad af gris", "1500-2100 g.", false)]
    public void Everyday_price_comes_from_text(string heading, string description, bool expected)
    {
        Assert.Equal(expected, Create(heading, description).IsEverydayPrice);
    }

    [Fact]
    public void Everyday_price_from_very_long_validity()
    {
        var offer = Create("Smør", from: "2026-05-02T22:00:00+00:00", to: "2026-12-31T22:59:59+00:00");
        Assert.True(offer.IsEverydayPrice);
    }

    [Fact]
    public void Christmas_menu_running_twelve_weeks_is_not_an_everyday_price()
    {
        var offer = Create("ANDEMENU", "Perfekt til julens gode stunder",
            from: "2026-10-07T22:00:00+00:00", to: "2026-12-31T22:59:59+00:00");
        Assert.False(offer.IsEverydayPrice);
    }

    [Theory]
    [InlineData("10 stk. Str. M/L.\nGælder kun med Netto+ appen", true)]
    [InlineData("Medlemspris. 500 g", true)]
    [InlineData("Klasse 1. 1 kg. Pr. kg 15,00", false)]
    public void Requires_app_or_membership(string description, bool expected)
    {
        Assert.Equal(expected, Create("Vare", description).RequiresAppOrMembership);
    }

    [Theory]
    [InlineData("500-850 g. Kg-pris maks. 140,00. Maks. 3  poser pr. kunde. 1 pose", 3)]
    [InlineData("Max 2 stk. pr. kunde", 2)]
    [InlineData("Kg-pris maks. 140,48. 4 stk.", null)]
    public void Max_per_customer(string description, int? expected)
    {
        Assert.Equal(expected, Create("Vare", description).MaxPerCustomer);
    }

    [Fact]
    public void Automatic_matches_never_overwrite_a_manual_mapping()
    {
        var offer = Create("Kyllingebryst");
        offer.MapManually([7]);

        var changed = offer.SetMatchedIngredients([1, 2]);

        Assert.False(changed);
        Assert.Equal([7], offer.Ingredients.Select(i => i.IngredientId));
    }

    [Fact]
    public void Automatic_matches_replace_previous_automatic_matches()
    {
        var offer = Create("Flæsk i skiver eller koteletter");
        offer.SetMatchedIngredients([1, 2]);

        Assert.True(offer.SetMatchedIngredients([2, 3]));
        Assert.Equal([2, 3], offer.Ingredients.Select(i => i.IngredientId).Order());
        Assert.False(offer.SetMatchedIngredients([3, 2]));
    }
}
