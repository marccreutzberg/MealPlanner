using System.ComponentModel;
using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Meals;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Features.MealPlans;
using MealPlanner.Api.Features.Recipes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MealPlanner.Api.Mcp;

/// <summary>MCP tools for recipes and meal plans (spec 5 and 6). Same handlers as the HTTP endpoints.</summary>
[McpServerToolType]
public sealed class MealTools(IHttpContextAccessor http)
{
    private ClaimsPrincipal User => (http.HttpContext ?? throw new McpException("No request context")).User;

    [McpServerTool(Name = "add_recipe", Title = "Save a recipe", Destructive = false, Idempotent = true)]
    [Description("Saves a dish for the household. Write recipes yourself (no copied methods from websites). Use Danish " +
                 "names and ingredient texts ('kyllingebryst', 'hakket oksekød 8-12%'), amounts as text ('500 g', '2 dl', " +
                 "'1 dåse'), and mark the main ingredients (meat, fish, the vegetables the dish is about) with isMain: " +
                 "suggest_meals scores dishes by them. Staples like salt and oil are not main. The method is short text. " +
                 "Saving a name that exists replaces that recipe. Returns which lines matched a known ingredient.")]
    public async Task<AddRecipe.Response> AddRecipeAsync(
        AddRecipe.Handler handler,
        [Description("Dish name in Danish, e.g. 'Kylling i karry med ris'.")] string name,
        [Description("Number of servings the amounts are for.")] int servings,
        [Description("Total time in minutes.")] int totalMinutes,
        [Description("Suitable for children.")] bool isChildFriendly,
        [Description("Ingredient lines.")] List<AddRecipe.Line> ingredients,
        [Description("Short method.")] string? method = null,
        CancellationToken cancellationToken = default) =>
        Unwrap(await handler.HandleAsync(new AddRecipe.Command(name, servings, totalMinutes, isChildFriendly, ingredients, method),
            User.GetHouseholdId(), cancellationToken));

    [McpServerTool(Name = "suggest_meals", Title = "Suggest meals from the offers", ReadOnly = true, Idempotent = true)]
    [Description("Ranks the household's saved recipes by how many main ingredients are on offer in the coming week " +
                 "(home store REMA 1000 counts a little extra, cheaper wins a tie), leaving out dishes eaten in the last " +
                 "14 days. Each suggestion shows store and offer price per main ingredient. If too few recipes come back, " +
                 "look at search_offers for meat, fish and vegetables, write new dishes around them with add_recipe, and " +
                 "call this again.")]
    public async Task<SuggestMeals.Response> SuggestMealsAsync(
        SuggestMeals.Handler handler,
        [Description("How many dishes, 1-30.")] int count = 5,
        [Description("Only child-friendly dishes.")] bool? childFriendly = null,
        [Description("Max total minutes.")] int? maxMinutes = null,
        [Description("First day of the 7-day offer period (yyyy-MM-dd); default today.")] DateOnly? from = null,
        CancellationToken cancellationToken = default) =>
        Unwrap(await handler.HandleAsync(new SuggestMeals.Query(count, childFriendly, maxMinutes, from), User.GetHouseholdId(),
            cancellationToken));

    [McpServerTool(Name = "save_meal_plan", Title = "Save the meal plan", Destructive = false, Idempotent = true)]
    [Description("Sets the dish for days in a Monday-to-Sunday week. Give recipes by id (from suggest_meals) or exact " +
                 "name. Only the given days change; a day with neither id nor name is cleared. Then offer to put the " +
                 "ingredients on the shopping list with build_shopping_list(mealPlanWeek).")]
    public async Task<MealPlanViews.MealPlanDto> SaveMealPlanAsync(
        SaveMealPlan.Handler handler,
        [Description("Any day in the week (yyyy-MM-dd).")] DateOnly week,
        [Description("Days to set: date plus recipeId or recipe name.")] List<SaveMealPlan.Day> days,
        CancellationToken cancellationToken = default) =>
        Unwrap(await handler.HandleAsync(new SaveMealPlan.Command(week, days), User.GetHouseholdId(), cancellationToken));

    [McpServerTool(Name = "get_meal_plan", Title = "Get the meal plan", ReadOnly = true, Idempotent = true)]
    [Description("The meal plan for the week containing a day: seven days with the dish (ingredients and method) and " +
                 "whether its ingredients are already on the shopping list.")]
    public Task<MealPlanViews.MealPlanDto> GetMealPlanAsync(
        GetMealPlan.Handler handler,
        [Description("Any day in the week (yyyy-MM-dd).")] DateOnly week,
        CancellationToken cancellationToken = default) =>
        handler.HandleAsync(week, User.GetHouseholdId(), cancellationToken);

    private static T Unwrap<T>(Result<T> result) =>
        result.IsSuccess ? result.Value! : throw new McpException(result.Error!.Message);
}
