using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;

namespace MealPlanner.Tests.Common;

/// <summary>Cases from spec 12.4 and real headings from Tjek (6/11 2026), against the real seed keywords.</summary>
public class IngredientMatcherTests
{
    private static readonly IngredientMatcher Matcher = new(SeedData.Ingredients.Select((i, index) =>
        new IngredientMatcher.Candidate(index, i.Name, [.. i.Keywords], [.. i.Exclusions])));

    private static readonly string[] Names = [.. SeedData.Ingredients.Select(i => i.Name)];

    private static string[] Match(string heading) => [.. Matcher.Match(heading).Select(id => Names[id]).Order()];

    [Theory]
    [InlineData("Dansk kyllingebrystfilet, -inderfilet eller -lårfilet", "Kyllingebryst|Kyllingelår")]
    [InlineData("MADVÆRKET Kyllingebrystfilet eller hele -lår med ryg", "Kyllingebryst|Kyllingelår")]
    [InlineData("Netto dansk flæsk i skiver eller koteletter", "Flæsk i skiver|Koteletter")]
    [InlineData("Herregårds-rødkål, Meny chips el. Kims franske kartofler", "Rødkål")]
    [InlineData("Hakket grise- og kalvekød", "Hakket gris & kalv")]
    [InlineData("Dansk mørbrad af gris", "Svinemørbrad")]
    [InlineData("MADVÆRKET Piskefløde", "Fløde")]
    [InlineData("Laksefilet", "Laks")]
    [InlineData("Arla letmælk", "Mælk")]
    [InlineData("Cocio kakaomælk", "Kakaomælk")]
    [InlineData("Coop flødekartofler*", "Flødekartofler")]
    [InlineData("Bagekartofler", "Kartofler")]
    [InlineData("Danske frilandsæg", "Æg")]
    [InlineData("Basmati ris", "Ris")]
    [InlineData("FRESHONA Broccoli, blomkål eller rosenkål", "Broccoli")]
    [InlineData("Cocio  eller Blue Keld", "Kakaomælk")]
    public void Matches(string heading, string expected)
    {
        Assert.Equal(expected.Split('|'), Match(heading));
    }

    [Theory]
    [InlineData("Kyllingebrystfilet pålæg")]
    [InlineData("REMA 1000 Kartofler i glas eller svesker")]
    [InlineData("Kokosmælk")]
    [InlineData("Røget laks")]
    [InlineData("Flødeboller")]
    [InlineData("Hytte pålæg")]
    [InlineData("Salling Princip brioche, god pris")]
    [InlineData("Risalamande")]
    [InlineData("❤️xl bøf 300g 🤩‼️")]
    // False positives seen in the first live fetch (6/11 2026).
    [InlineData("Sensodyne Tandpasta eller tandbørster soft")]
    [InlineData("Vaniljepasta eller vaniljestænger")]
    [InlineData("P-tærter, Carletti juleskum eller Skumbananer")]
    [InlineData("Toiletpapirholder")]
    [InlineData("Flæskestegs sandwich med chilimayo")]
    [InlineData("MADVÆRKET Flødehavarti")]
    [InlineData("MILBONA Madlavningsjævner med fløde")]
    [InlineData("Tulip Pålækker pålæg, baconpostej eller salami")]
    [InlineData("ØGO økologiske søde kartofler")]
    [InlineData("Spejle æg")]
    [InlineData("Nescafé kaffe specialiteter el. Nesquik kakao")]
    [InlineData("Jensens æbleskiver med kakaocreme")]
    [InlineData("JENSENS Ande- eller flæskestegssauce")]
    // False positives seen in meal suggestions (6/11 2026).
    [InlineData("Rahbek snack fileter, indbagt laks")]
    [InlineData("VITASIA Forstegt kyllingebryst")]
    [InlineData("FRESHONA Broccoli- eller blomkålsris")]
    [InlineData("")]
    public void Does_not_match(string heading)
    {
        Assert.Empty(Match(heading));
    }

    [Fact]
    public void Normalize_removes_symbols_and_keeps_separators()
    {
        Assert.Equal("coop flødekartofler", IngredientMatcher.Normalize("Coop  flødekartofler*"));
        Assert.Equal("gris & kalv, eller -lår", IngredientMatcher.Normalize("Gris & Kalv, eller -lår 🤩"));
    }
}
