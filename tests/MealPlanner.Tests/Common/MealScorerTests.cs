using MealPlanner.Api.Common.Meals;
using static MealPlanner.Api.Common.Meals.MealScorer;

namespace MealPlanner.Tests.Common;

public class MealScorerTests
{
    private static readonly DateOnly Day = new(2026, 11, 12);

    private static Offer At(string store, decimal price) => new(1, store, "", price, Day, store == "REMA 1000");

    private static Candidate Recipe(string name, params Main[] mains) => new(1, name, 30, true, [.. mains]);

    private static Main M(string text, params Offer[] offers) => new(text, null, [.. offers]);

    [Fact]
    public void More_mains_on_offer_ranks_higher()
    {
        var ranked = Rank(
        [
            Recipe("Lasagne", M("hakket oksekød", At("Netto", 30)), M("lasagneplader"), M("tomater")),
            Recipe("Kylling i karry", M("kyllingebryst", At("Lidl", 28)), M("ris", At("Lidl", 10))),
        ]);

        Assert.Equal(["Kylling i karry", "Lasagne"], ranked.Select(r => r.Recipe.Name));
        Assert.Equal(1.0m, ranked[0].Score);
        Assert.Equal(2, ranked[0].OnOffer);
    }

    [Fact]
    public void Tie_goes_to_the_cheaper_dish()
    {
        var ranked = Rank(
        [
            Recipe("Dyr", M("oksefilet", At("Kvickly", 120))),
            Recipe("Billig", M("hakket grisekød", At("Netto", 25))),
        ]);

        Assert.Equal("Billig", ranked[0].Recipe.Name);
    }

    [Fact]
    public void Home_store_offers_weigh_a_little_more()
    {
        var ranked = Rank(
        [
            Recipe("Andet sted", M("laks", At("Lidl", 40))),
            Recipe("Hjemme", M("laks", At("REMA 1000", 45))),
        ]);

        Assert.Equal("Hjemme", ranked[0].Recipe.Name);
        Assert.Equal(1.1m, ranked[0].Score);
    }

    [Fact]
    public void Best_offer_is_the_cheapest_and_home_wins_a_tie()
    {
        var scored = Score(Recipe("X", M("smør", At("Lidl", 10), At("REMA 1000", 10), At("Netto", 12))));
        Assert.Equal("REMA 1000", scored.Mains[0].BestOffer!.Store);
    }

    [Fact]
    public void A_bonus_never_beats_a_better_share()
    {
        var ranked = Rank(
        [
            Recipe("Halvt hjemme", M("a", At("REMA 1000", 10)), M("b")),
            Recipe("Alt på tilbud", M("a", At("Lidl", 10)), M("b", At("Lidl", 10))),
        ]);
        Assert.Equal("Alt på tilbud", ranked[0].Recipe.Name);
    }
}
