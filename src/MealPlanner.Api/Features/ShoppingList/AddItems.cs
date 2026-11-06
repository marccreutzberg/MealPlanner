using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Results;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>
/// Adds items to the list (spec 4); the work is done by <see cref="ShoppingListWriter"/>.
/// POST /api/shopping-list/items and add_to_shopping_list.
/// </summary>
public static class AddItems
{
    public sealed record Command(List<ShoppingListWriter.NewItem> Items);

    public sealed record Response(List<ShoppingListViews.ItemDto> Items, List<string>? Warnings = null);

    /// <summary>Warnings for Claude about items that look like several products in one.</summary>
    public static List<string> Warnings(IEnumerable<Domain.ShoppingListItem> items) => [];

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/shopping-list/items", async (Command command, ClaimsPrincipal user, Handler handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(command, user.GetHouseholdId(), user.GetUserId(), ct)).ToHttpResult());

    public sealed class Handler(AppDbContext db, ShoppingListWriter writer, ListNotifier notifier)
    {
        public async Task<Result<Response>> HandleAsync(Command command, int householdId, string userId, CancellationToken ct)
        {
            var result = await writer.AddAsync(command.Items ?? [], householdId, userId, ct);
            if (!result.IsSuccess) return result.Error!;
            await notifier.ListChangedAsync(householdId, ct);

            var written = result.Value!;
            var stores = written.Stores.ToDictionary(s => s.Id);
            var ingredientNames = await ShoppingListViews.LoadIngredientNamesAsync(db, written.Items, ct);
            return new Response([.. written.Items.Select(i => ShoppingListViews.ToDto(i, written.Offers, stores, ingredientNames))]);
        }
    }
}
