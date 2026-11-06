using MealPlanner.Api.Common.Search;

namespace MealPlanner.Tests.Common;

public class FuzzySearchTests
{
    [Theory]
    [InlineData("kyllingebryst", "Kyllingebryst")]
    [InlineData("hytte ost", "Hytteost")]
    [InlineData("roedkaal", "Rødkål")]
    [InlineData("kylling", "Kyllingebryst")]
    [InlineData("hyteost", "Hytteost")]          // one typo in a short-ish word
    [InlineData("kyllingbryst", "Kyllingebryst")] // missing letter in a long word
    [InlineData("kyllnigebryst", "Kyllingebryst")] // swapped letters
    [InlineData("hakket okse", "REMA 1000 Hakket dansk oksekød 15-18%")]
    [InlineData("laks", "Skagenfood fersk laksestykke")]
    public void Matches(string query, string text)
    {
        Assert.True(FuzzySearch.Score(query, text) > 0);
    }

    [Theory]
    [InlineData("kylling", "Hakket oksekød")]
    [InlineData("ost", "Rødkål")]
    [InlineData("æg", "Pålæg")]   // too short for typos or substring matches
    [InlineData("", "Rødkål")]
    public void Does_not_match(string query, string text)
    {
        Assert.Equal(0, FuzzySearch.Score(query, text));
    }

    [Fact]
    public void One_typo_ranks_above_two()
    {
        Assert.True(FuzzySearch.Score("kyllingbrst", "Kyllingebryst") < FuzzySearch.Score("kyllingbryst", "Kyllingebryst"));
        Assert.Equal(0, FuzzySearch.Score("hyteost", "Rygeost")); // two typos in a 7-letter word
    }

    [Fact]
    public void Ranks_exact_then_prefix_then_contains_then_typo()
    {
        var exact = FuzzySearch.Score("smør", "Smør");
        var prefix = FuzzySearch.Score("smør", "Smørbar");
        var contains = FuzzySearch.Score("smør", "Lurpak smør");
        var typo = FuzzySearch.Score("smøt", "Lurpak smør");

        Assert.True(exact > prefix, $"{exact} > {prefix}");
        Assert.True(prefix > contains, $"{prefix} > {contains}");
        Assert.True(contains > typo, $"{contains} > {typo}");
        Assert.True(typo > 0);
    }

    [Theory]
    [InlineData("ca", "ac", 1)]
    [InlineData("hyteost", "hytteost", 1)]
    [InlineData("kitten", "sitting", 3)]
    public void Damerau_levenshtein(string a, string b, int expected)
    {
        Assert.Equal(expected, FuzzySearch.DamerauLevenshtein(a, b));
    }
}
