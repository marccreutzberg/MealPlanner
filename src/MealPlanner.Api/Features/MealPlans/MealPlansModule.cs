namespace MealPlanner.Api.Features.MealPlans;

public static class MealPlansModule
{
    public static IServiceCollection AddMealPlans(this IServiceCollection services)
    {
        services.AddScoped<SaveMealPlan.Handler>();
        services.AddScoped<GetMealPlan.Handler>();
        services.AddScoped<AddMealPlanToList.Handler>();
        return services;
    }

    /// <summary>Meal plan endpoints accept the cookie or an API key.</summary>
    public static IEndpointRouteBuilder MapMealPlans(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").RequireAuthorization();
        SaveMealPlan.Map(group);
        GetMealPlan.Map(group);
        AddMealPlanToList.Map(group);
        return app;
    }
}
