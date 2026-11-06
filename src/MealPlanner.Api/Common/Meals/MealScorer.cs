namespace MealPlanner.Api.Common.Meals;

/// <summary>
/// Ranks recipes against the offers (spec 5.3): the share of main ingredients on offer in the period decides,
/// offers in the home store count a little extra, and on a tie the cheaper dish wins. Pure; no database.
/// </summary>
public static class MealScorer
{
    /// <summary>A recipe whose main ingredients are all on offer in the home store gets 10 % on top.</summary>
    public const decimal HomeStoreBonus = 0.10m;

    public sealed record Offer(int Id, string Store, string Heading, decimal Price, DateOnly LastValidDay, bool IsHomeStore);

    public sealed record Main(string Text, string? Ingredient, List<Offer> Offers);

    public sealed record Candidate(int RecipeId, string Name, int TotalMinutes, bool IsChildFriendly, List<Main> Mains);

    public sealed record ScoredMain(string Text, string? Ingredient, Offer? BestOffer);

    public sealed record Scored(Candidate Recipe, decimal Score, decimal OfferTotal, List<ScoredMain> Mains)
    {
        public int OnOffer => Mains.Count(m => m.BestOffer is not null);
    }

    public static List<Scored> Rank(IEnumerable<Candidate> candidates) =>
        [.. candidates
            .Select(Score)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.OfferTotal)
            .ThenBy(s => s.Recipe.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("da-DK"), true))];

    public static Scored Score(Candidate recipe)
    {
        var mains = recipe.Mains
            .Select(m => new ScoredMain(m.Text, m.Ingredient,
                // Cheapest offer; the home store wins a tie.
                m.Offers.OrderBy(o => o.Price).ThenBy(o => o.IsHomeStore ? 0 : 1).FirstOrDefault()))
            .ToList();
        if (mains.Count == 0) return new Scored(recipe, 0, 0, mains);

        var share = (decimal)mains.Count(m => m.BestOffer is not null) / mains.Count;
        var homeShare = (decimal)recipe.Mains.Count(m => m.Offers.Any(o => o.IsHomeStore)) / mains.Count;
        var score = decimal.Round(share + HomeStoreBonus * homeShare, 3);
        var total = mains.Sum(m => m.BestOffer?.Price ?? 0);
        return new Scored(recipe, score, total, mains);
    }
}
