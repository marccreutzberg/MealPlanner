namespace MealPlanner.Api.Features.Staples;

public static class StaplesModule
{
    public static IServiceCollection AddStaples(this IServiceCollection services)
    {
        services.AddScoped<GetStapleSuggestions.Handler>();
        return services;
    }

    /// <summary>Staple endpoints accept the cookie or an API key.</summary>
    public static IEndpointRouteBuilder MapStaples(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").RequireAuthorization();
        GetStapleSuggestions.Map(group);
        ManageStaples.Map(group);
        return app;
    }
}
