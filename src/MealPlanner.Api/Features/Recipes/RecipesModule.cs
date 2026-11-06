namespace MealPlanner.Api.Features.Recipes;

public static class RecipesModule
{
    public static IServiceCollection AddRecipes(this IServiceCollection services)
    {
        services.AddScoped<AddRecipe.Handler>();
        services.AddScoped<SuggestMeals.Handler>();
        return services;
    }

    /// <summary>Recipe endpoints accept the cookie or an API key.</summary>
    public static IEndpointRouteBuilder MapRecipes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").RequireAuthorization();
        AddRecipe.Map(group);
        SuggestMeals.Map(group);
        GetRecipes.Map(group);
        return app;
    }
}
