using MealPlanner.Api.Domain;

namespace MealPlanner.Tests.Domain;

public class QuantityTests
{
    [Fact]
    public void Spec_5_5_butter_across_recipes() =>
        Assert.Equal("ca. 1 kg", Quantity.Sum(["600 g", "250 g", "175 g"]));

    [Theory]
    [InlineData("200 g", "300 g", "500 g")]
    [InlineData("1 kg", "500 g", "1,5 kg")]
    [InlineData("2 dl", "3 dl", "5 dl")]
    [InlineData("1 l", "5 dl", "1,5 l")]
    [InlineData("2 spsk", "1 tsk", "35 ml")]
    [InlineData("3 stk.", "2", "5 stk.")]
    [InlineData("1 dåse", "1 dåse", "2 dåser")]
    [InlineData("2 fed", "3 fed", "5 fed")]
    [InlineData("½ l", "½ l", "1 l")]
    [InlineData("1½ dl", "½ dl", "2 dl")]
    public void Adds_same_kind(string a, string b, string expected) => Assert.Equal(expected, Quantity.Combine(a, b));

    [Theory]
    [InlineData("2 dl", "1 stk.", "2 dl + 1 stk.")]
    [InlineData("1 dåse", "400 g", "1 dåse + 400 g")]
    [InlineData("efter smag", "1 tsk", "efter smag + 1 tsk")]
    [InlineData("1 knivspids", "1 knivspids", "1 knivspids + 1 knivspids")]
    public void Keeps_different_kinds_apart(string a, string b, string expected) => Assert.Equal(expected, Quantity.Combine(a, b));

    [Fact]
    public void Merges_into_an_existing_list_of_amounts() =>
        Assert.Equal("4 dl + 1 stk.", Quantity.Combine("2 dl + 1 stk.", "2 dl"));

    [Theory]
    [InlineData(null, "2 dl", "2 dl")]
    [InlineData("2 dl", null, "2 dl")]
    [InlineData(null, null, null)]
    public void Empty_sides(string? a, string? b, string? expected) => Assert.Equal(expected, Quantity.Combine(a, b));

    [Fact]
    public void Approximate_stays_approximate() => Assert.Equal("ca. 600 g", Quantity.Combine("ca. 500 g", "100 g"));
}
