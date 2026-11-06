using MealPlanner.Api.Domain;

namespace MealPlanner.Api.Common.Meals;

/// <summary>Recipes as the app and Claude see them. Shared by the recipe and meal plan slices.</summary>
public static class RecipeViews
{
    public sealed record IngredientDto(string Text, string? Quantity, bool IsMain, string? Ingredient);

    public sealed record RecipeDto(int Id, string Name, int Servings, int TotalMinutes, bool IsChildFriendly, string? Method,
        List<IngredientDto> Ingredients);

    public static RecipeDto ToDto(Recipe recipe, IReadOnlyDictionary<int, string> ingredientNames) =>
        new(recipe.Id, recipe.Name, recipe.Servings, recipe.TotalMinutes, recipe.IsChildFriendly, recipe.Method,
            [.. recipe.Ingredients.Select(i => new IngredientDto(i.Text, i.Quantity, i.IsMain,
                i.IngredientId is { } id ? ingredientNames.GetValueOrDefault(id) : null))]);
}
