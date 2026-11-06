using System.ComponentModel;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Features.Offers;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MealPlanner.Api.Mcp;

/// <summary>
/// MCP tools for offers. Each tool calls the same handler as the HTTP endpoint (spec 6), scoped to the
/// household of the API key's user.
/// </summary>
[McpServerToolType]
public sealed class OfferTools(IHttpContextAccessor http)
{
    private int HouseholdId => (http.HttpContext ?? throw new McpException("No request context")).User.GetHouseholdId();

    [McpServerTool(Name = "search_offers", Title = "Search offers", ReadOnly = true, Idempotent = true)]
    [Description("Searches this week's grocery offers from REMA 1000, Netto, Kvickly, Løvbjerg, Lidl and Coop 365 near the " +
                 "household. Texts are Danish. Fuzzy search on the offer heading and the matched ingredient " +
                 "(e.g. 'kyllingebryst', 'hakket oksekød', 'smør'). Each offer has chain, price, pre-price, first and " +
                 "last valid day, matched ingredients and conditions (app/membership required, max per customer). " +
                 "isEverydayPrice means a permanent low price (Lidl 'Fast lav pris'), not a campaign.")]
    public async Task<SearchOffers.Response> SearchOffersAsync(
        SearchOffers.Handler handler,
        [Description("Free text in Danish. Leave empty to list offers.")] string? text = null,
        [Description("Part of a chain name, e.g. 'rema', 'netto', 'lidl', '365'.")] string? store = null,
        [Description("'today' = valid today, 'week' = valid on any of the next 7 days (default), 'upcoming' = starts after today.")] string period = "week",
        [Description("Only offers without an ingredient and not marked by hand; use this to find offers to map.")] bool onlyUnmapped = false,
        [Description("Max offers to return, 1-200.")] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        if (!SearchOffers.TryParsePeriod(period, out var parsed))
            throw new McpException("period must be 'today', 'week' or 'upcoming'");

        var result = await handler.HandleAsync(new SearchOffers.Query(text, store, parsed, onlyUnmapped, limit),
            HouseholdId, cancellationToken);
        return Unwrap(result);
    }

    [McpServerTool(Name = "map_offer", Title = "Map offer to ingredients", Destructive = false, Idempotent = true)]
    [Description("Maps an offer to one or more ingredients by hand, e.g. after search_offers with onlyUnmapped. " +
                 "The mapping is never overwritten by the daily fetch. With exactly one ingredient a keyword is saved " +
                 "(default: the first part of the heading) and all other offers are re-matched, so similar offers get " +
                 "the ingredient too. Pass an empty list to mark non-food or a wrong match as 'no ingredient'. " +
                 "Unknown ingredient names are created when a category is given (Danish category names: Frugt og grønt, " +
                 "Brød, Kød, fisk og fjerkræ, Pålæg, Mejeri, Kolonial, Frost, Drikkevarer, Slik og snacks, Husholdning, " +
                 "Personlig pleje, Andet).")]
    public async Task<MapOffer.Response> MapOfferAsync(
        MapOffer.Handler handler,
        [Description("Offer id from search_offers.")] int offerId,
        [Description("Ingredient names in Danish, e.g. ['Smør']. Empty list = no ingredient.")] List<string> ingredients,
        [Description("Keyword to save instead of the heading's first part, e.g. 'smør'. Only with one ingredient.")] string? keyword = null,
        [Description("Category for ingredients that do not exist yet.")] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(new MapOffer.Command(offerId, ingredients, keyword, category),
            HouseholdId, cancellationToken);
        return Unwrap(result);
    }

    // Expected errors go back to Claude as a readable tool error.
    private static T Unwrap<T>(Result<T> result) =>
        result.IsSuccess ? result.Value! : throw new McpException(result.Error!.Message);
}
