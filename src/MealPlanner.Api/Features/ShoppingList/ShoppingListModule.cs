using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Realtime;

namespace MealPlanner.Api.Features.ShoppingList;

public static class ShoppingListModule
{
    public static IServiceCollection AddShoppingList(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<ListNotifier>();
        services.AddScoped<ShoppingListWriter>();
        services.AddScoped<GetList.Handler>();
        services.AddScoped<GetBought.Handler>();
        services.AddScoped<AddItems.Handler>();
        services.AddScoped<UpdateItem.Handler>();
        services.AddScoped<SetBought.Handler>();
        return services;
    }

    /// <summary>All list endpoints accept the cookie or an API key; the realtime hub is for the app (cookie).</summary>
    public static IEndpointRouteBuilder MapShoppingList(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").RequireAuthorization();
        GetList.Map(group);
        GetBought.Map(group);
        AddItems.Map(group);
        UpdateItem.Map(group);
        SetBought.Map(group);
        RemoveItems.Map(group);
        SuggestItems.Map(group);
        app.MapHub<ShoppingListHub>(ShoppingListHub.Path);
        return app;
    }
}
