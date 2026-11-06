using System.ComponentModel;
using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Features.ShoppingList;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MealPlanner.Api.Mcp;

/// <summary>MCP tools for the shopping list (spec 6). Same handlers as the /api/shopping-list endpoints.</summary>
[McpServerToolType]
public sealed class ShoppingListTools(IHttpContextAccessor http)
{
    private ClaimsPrincipal User => (http.HttpContext ?? throw new McpException("No request context")).User;

    [McpServerTool(Name = "get_shopping_list", Title = "Get shopping list", ReadOnly = true, Idempotent = true)]
    [Description("The household's shared shopping list grouped by category (in the home store's walking order). " +
                 "Each item has quantity, variant/brand, tag, store flag and the best offer valid today (store, price, " +
                 "last valid day). Also lists the household's stores with ids. Texts are Danish.")]
    public Task<ShoppingListViews.ListDto> GetShoppingListAsync(GetList.Handler handler, CancellationToken cancellationToken = default) =>
        handler.HandleAsync(User.GetHouseholdId(), cancellationToken);

    [McpServerTool(Name = "add_to_shopping_list", Title = "Add to shopping list", Destructive = false)]
    [Description("Adds items to the shared list, e.g. from a screenshot of another list or a recipe. Category and " +
                 "offers are found automatically. An item already on the list gets the amounts combined. Use Danish " +
                 "item names ('Smør', 'Hvedemel'), the amount as text ('5 stk.', '2 kg'), a variant/brand when it matters " +
                 "('Castello', 'rigtigt smør, ikke smørbar', 'pålæg'), a tag to group items on the trip ('Bagning'), " +
                 "a store to buy it in ('Netto'), or an offer id from search_offers to flag it to that offer.")]
    public async Task<AddItems.Response> AddToShoppingListAsync(
        AddItems.Handler handler,
        [Description("Items to add.")] List<AddItems.NewItem> items,
        CancellationToken cancellationToken = default) =>
        Unwrap(await handler.HandleAsync(new AddItems.Command(items), User.GetHouseholdId(), User.GetUserId(), cancellationToken));

    [McpServerTool(Name = "update_shopping_list_item", Title = "Correct a shopping list item", Destructive = false, Idempotent = true)]
    [Description("Corrects an item after the fact ('det er rigtigt smør', 'havarti er Castello', 'hamburgerryg er pålæg') " +
                 "and recalculates its offers. Omitted fields stay as they are; an empty string clears quantity, variant " +
                 "or tag. categoryId and storeId come from get_shopping_list. rememberStore makes the item's flagged " +
                 "store a standing choice for the household ('bleer køber vi altid i Netto').")]
    public async Task<ShoppingListViews.ItemDto> UpdateShoppingListItemAsync(
        UpdateItem.Handler handler,
        [Description("Item id from get_shopping_list.")] int itemId,
        string? name = null,
        string? quantity = null,
        string? variant = null,
        string? tag = null,
        [Description("Move to this category; saved as a learned rule.")] int? categoryId = null,
        [Description("Flag the item to this store.")] int? storeId = null,
        [Description("Remove the store flag.")] bool clearFlag = false,
        [Description("Remember the flagged store for this item from now on.")] bool rememberStore = false,
        CancellationToken cancellationToken = default) =>
        Unwrap(await handler.HandleAsync(
            new UpdateItem.Command(itemId, name, quantity, variant, tag, categoryId, storeId, clearFlag, rememberStore),
            User.GetHouseholdId(), cancellationToken));

    [McpServerTool(Name = "build_shopping_list", Title = "Plan the shopping trip", Destructive = false)]
    [Description("Plans the shopping trip from the list and today's offers: REMA 1000 always, the store (or stores next " +
                 "to each other) that saves most as number two, a third only when it saves more than the household's " +
                 "threshold, and items flagged to a store always there. Optionally adds extra items first. Returns the " +
                 "items per store in driving order with known prices, subtotals, an estimated saving and the trip as a " +
                 "plain-text checklist to show the user as is.")]
    public async Task<BuildTrip.Trip> BuildShoppingListAsync(
        AddItems.Handler addItems,
        BuildTrip.Handler buildTrip,
        [Description("Items to add before planning; same fields as add_to_shopping_list.")] List<AddItems.NewItem>? extraItems = null,
        CancellationToken cancellationToken = default)
    {
        if (extraItems is { Count: > 0 })
            Unwrap(await addItems.HandleAsync(new AddItems.Command(extraItems), User.GetHouseholdId(), User.GetUserId(), cancellationToken));
        return await buildTrip.HandleAsync(User.GetHouseholdId(), cancellationToken);
    }

    private static T Unwrap<T>(Result<T> result) =>
        result.IsSuccess ? result.Value! : throw new McpException(result.Error!.Message);
}
