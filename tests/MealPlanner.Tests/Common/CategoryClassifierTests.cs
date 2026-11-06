using MealPlanner.Api.Common.Categories;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;

namespace MealPlanner.Tests.Common;

/// <summary>Against the real seed dictionary; names from the shopping trip in spec 12.6.</summary>
public class CategoryClassifierTests
{
    // Category id = position in SeedData.Categories + 1; ingredient id = position in SeedData.Ingredients.
    private static readonly Dictionary<string, int> CategoryIds =
        SeedData.Categories.Select((c, i) => (c.Name, Id: i + 1)).ToDictionary(c => c.Name, c => c.Id);

    private static CategoryClassifier Create(params CategoryClassifier.Rule[] householdRules)
    {
        var shared = SeedData.CategoryTerms
            .SelectMany(c => c.Terms.Select(t => new CategoryClassifier.Rule(t, CategoryIds[c.Category], false)));
        var matcher = new IngredientMatcher(SeedData.Ingredients.Select((i, index) =>
            new IngredientMatcher.Candidate(index, i.Name, [.. i.Keywords], [.. i.Exclusions])));
        var ingredientCategories = SeedData.Ingredients.Select((i, index) => (index, CategoryIds[i.Category]))
            .ToDictionary(x => x.index, x => x.Item2);
        return new CategoryClassifier([.. shared, .. householdRules], matcher, ingredientCategories, CategoryIds["Andet"]);
    }

    private static string CategoryOf(CategoryClassifier classifier, string item) =>
        CategoryIds.Single(c => c.Value == classifier.Classify(item).CategoryId).Key;

    [Theory]
    [InlineData("Smør", "Mejeri")]
    [InlineData("Lurpak", "Mejeri")]
    [InlineData("Sukker", "Kolonial")]
    [InlineData("Hvedemel", "Kolonial")]
    [InlineData("Hamburgerryg (pålæg)", "Pålæg")]
    [InlineData("Juice til børn", "Drikkevarer")]
    [InlineData("Fanta 1,5 l", "Drikkevarer")]
    [InlineData("Mælk", "Mejeri")]
    [InlineData("Æg", "Mejeri")]
    [InlineData("Marcipan", "Kolonial")]
    [InlineData("Kærnemælk", "Mejeri")]
    [InlineData("Flormelis", "Kolonial")]
    [InlineData("Kakao", "Kolonial")]
    [InlineData("Kanel", "Kolonial")]
    [InlineData("Citroner", "Frugt og grønt")]
    [InlineData("Blå birkes", "Kolonial")]
    [InlineData("Castello havarti", "Mejeri")]
    [InlineData("Fløde", "Mejeri")]
    [InlineData("Faxe Kondi", "Drikkevarer")]
    [InlineData("Pepsi Max", "Drikkevarer")]
    [InlineData("Pålægschokolade", "Pålæg")]
    [InlineData("Deo", "Personlig pleje")]
    [InlineData("Spegepølse", "Pålæg")]
    [InlineData("Ost", "Mejeri")]
    [InlineData("Natbleer", "Personlig pleje")]
    // Compound words fall back to their last part.
    [InlineData("Rosinbrød", "Brød")]
    [InlineData("Rygeost", "Mejeri")]
    [InlineData("Maltøl", "Drikkevarer")]
    [InlineData("Kyllingepålæg", "Pålæg")]
    [InlineData("Toiletpapir", "Husholdning")]
    [InlineData("Kattemad", "Andet")]
    [InlineData("Xyzzy", "Andet")]
    public void Classifies(string item, string expected)
    {
        Assert.Equal(expected, CategoryOf(Create(), item));
    }

    [Fact]
    public void Learned_household_rule_wins()
    {
        var classifier = Create(new CategoryClassifier.Rule("kakao", CategoryIds["Drikkevarer"], true));
        Assert.Equal("Drikkevarer", CategoryOf(classifier, "Kakao"));
    }

    [Fact]
    public void Reports_matched_ingredient()
    {
        var result = Create().Classify("Kyllingebrystfilet");
        Assert.Equal("Kyllingebryst", SeedData.Ingredients[result.IngredientId!.Value].Name);
    }

    [Fact]
    public void Short_words_are_not_matched_as_compound_tails()
    {
        // "is" (Frost) and "te" (Drikkevarer) must not catch "ris" or "chokoladeplade".
        Assert.Equal("Kolonial", CategoryOf(Create(), "Ris"));
    }
}
