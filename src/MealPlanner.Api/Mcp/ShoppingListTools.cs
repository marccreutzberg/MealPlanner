using System.ComponentModel;
using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Trips;
using MealPlanner.Api.Features.MealPlans;
using MealPlanner.Api.Features.ShoppingList;
using MealPlanner.Api.Features.Staples;
using MealPlanner.Api.Features.Trips;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MealPlanner.Api.Mcp;

/// <summary>MCP tools for the shopping list (spec 6). Same handlers as the /api/shopping-list endpoints.</summary>
[McpServerToolType]
public sealed class ShoppingListTools(IHttpContextAccessor http)
{
    private ClaimsPrincipal User => (http.HttpContext ?? throw new McpException("No request context")).User;

    [McpServerTool(Name = "get_shopping_list", Title = "Get shopping list", ReadOnly = true, Idempotent = true)]
    [Description("What is still to buy on the household's shared shopping list, grouped by category (in the home " +
                 "store's walking order). Items already ticked off are only counted (boughtCount). " +
                 "Each item has quantity, variant/brand, tag, store flag and the best offer valid today (store, price, " +
                 "last valid day). Also lists the household's stores with ids. Texts are Danish.")]
    public Task<ShoppingListViews.ListDto> GetShoppingListAsync(GetList.Handler handler, CancellationToken cancellationToken = default) =>
        handler.HandleAsync(User.GetHouseholdId(), cancellationToken);

    private const string OneProductPerItem =
        "One item per product. Never combine several products in one item ('Natron, bagepulver og vaniljesukker' " +
        "must be three items). quantity is an amount ('1 pk.', '500 g'), never a note — put notes like 'tjek skabet " +
        "først' in variant. The response has warnings for names that look like several products; split those.";

    [McpServerTool(Name = "add_to_shopping_list", Title = "Add to shopping list", Destructive = false)]
    [Description("Adds items to the shared list, e.g. from a screenshot of another list or a recipe. Category and " +
                 "offers are found automatically. An item already on the list gets the amounts combined. Use Danish " +
                 "item names ('Smør', 'Hvedemel'), the amount as text ('5 stk.', '2 kg'), a variant/brand when it matters " +
                 "('Castello', 'rigtigt smør, ikke smørbar', 'pålæg'), a tag to group items on the trip ('Bagning'), " +
                 "a store to buy it in ('Netto'), or an offer id from search_offers to flag it to that offer. " +
                 OneProductPerItem)]
    public async Task<AddItems.Response> AddToShoppingListAsync(
        AddItems.Handler handler,
        [Description("Items to add.")] List<ShoppingListWriter.NewItem> items,
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

    [McpServerTool(Name = "build_shopping_list", Title = "Create the shopping trip", Destructive = true)]
    [Description("Creates the shopping trip from everything on the list not bought yet, with today's offers, and " +
                 "finishes the trip in progress if there is one. Stores: REMA 1000 always, the store (or stores next " +
                 "to each other) that saves most as number two, a third only when it saves more than the household's " +
                 "threshold, and items flagged to a store always there; or only the stores named in 'stores' (plus " +
                 "REMA 1000). Optionally puts a week's meal plan on the list first (amounts summed across dishes; days " +
                 "already on the list are skipped) and adds extra items. Returns the trip: items per store in driving " +
                 "order with known prices, subtotals, an estimated saving and the trip as a plain-text checklist to show " +
                 "the user as is. To only look at the trip in progress, use get_trip instead: this replaces it. The " +
                 "family ticks items off in the app; change the trip with set_trip_stores and " +
                 "move_trip_item.")]
    public async Task<TripViews.TripDto> BuildShoppingListAsync(
        AddItems.Handler addItems,
        AddMealPlanToList.Handler addMealPlan,
        CreateTrip.Handler createTrip,
        AppDbContext db,
        [Description("Any day in a meal plan week (yyyy-MM-dd) whose ingredients should go on the list first.")] DateOnly? mealPlanWeek = null,
        [Description("Items to add before planning; same fields as add_to_shopping_list. " + OneProductPerItem)] List<ShoppingListWriter.NewItem>? extraItems = null,
        [Description("Only these stores (names, e.g. 'Lidl'); REMA 1000 is always included. Leave out to let the planner choose.")] List<string>? stores = null,
        CancellationToken cancellationToken = default)
    {
        var householdId = User.GetHouseholdId();
        List<int>? storeIds = null;
        if (stores is { Count: > 0 })
            storeIds = Unwrap(await StoreNames.ResolveAsync(db, householdId, stores, cancellationToken));

        if (mealPlanWeek is { } week)
        {
            var added = await addMealPlan.HandleAsync(week, force: false, householdId, User.GetUserId(), cancellationToken);
            // "Already on the list" is fine here; a missing plan is not.
            if (!added.IsSuccess && added.Error!.Kind == ErrorKind.NotFound) throw new McpException(added.Error.Message);
        }
        List<string>? warnings = null;
        if (extraItems is { Count: > 0 })
            warnings = Unwrap(await addItems.HandleAsync(new AddItems.Command(extraItems), householdId, User.GetUserId(), cancellationToken)).Warnings;
        var trip = Unwrap(await createTrip.HandleAsync(new CreateTrip.Command(storeIds), householdId, User.GetUserId(), cancellationToken));
        return warnings is { Count: > 0 } ? trip with { Warnings = warnings } : trip;
    }

    [McpServerTool(Name = "suggest_staples", Title = "What do we usually buy?", ReadOnly = true, Idempotent = true)]
    [Description("\"Plejer I at købe?\": items the household pinned as always needed, and items due again by their buying " +
                 "rhythm (median days between purchases; due at 80 %), with the usual amount and an offer badge when on " +
                 "offer today. Items already on the list or dismissed with 'har nok' are left out. Offer them to the user " +
                 "and add the chosen ones with add_to_shopping_list.")]
    public Task<List<GetStapleSuggestions.SuggestionDto>> SuggestStaplesAsync(
        GetStapleSuggestions.Handler handler, CancellationToken cancellationToken = default) =>
        handler.HandleAsync(User.GetHouseholdId(), cancellationToken);

    private static T Unwrap<T>(Result<T> result) =>
        result.IsSuccess ? result.Value! : throw new McpException(result.Error!.Message);
}
