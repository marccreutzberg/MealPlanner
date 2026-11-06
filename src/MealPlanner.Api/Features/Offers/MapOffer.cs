using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Common.Search;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.Offers;

/// <summary>
/// Manual mapping of an offer to ingredients (spec 3.4). Used by POST /api/offers/{id}/mapping and map_offer.
/// The mapping is never overwritten by the fetch job, and the heading is saved as a new keyword so the
/// matcher finds the product by itself next time.
/// </summary>
public static class MapOffer
{
    /// <param name="Ingredients">Ingredient names. Empty marks the offer as "no ingredient".</param>
    /// <param name="Keyword">Keyword to save; defaults to the first part of the heading. Only with one ingredient.</param>
    /// <param name="Category">Category for ingredients that do not exist yet; without it unknown names are an error.</param>
    public sealed record Command(int OfferId, List<string> Ingredients, string? Keyword = null, string? Category = null);

    public sealed record Body(List<string> Ingredients, string? Keyword = null, string? Category = null);

    public sealed record Response(int OfferId, string Heading, List<string> Ingredients, string? KeywordSaved,
        List<string> CreatedIngredients, int OtherOffersRematched);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/offers/{id:int}/mapping", async (int id, Body body, ClaimsPrincipal user, Handler handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(new Command(id, body.Ingredients, body.Keyword, body.Category),
                user.GetHouseholdId(), ct)).ToHttpResult())
            .RequireAuthorization();

    public sealed class Handler(AppDbContext db)
    {
        public async Task<Result<Response>> HandleAsync(Command command, int householdId, CancellationToken ct)
        {
            var offer = await db.Offers.Include(o => o.Ingredients).SingleOrDefaultAsync(o => o.Id == command.OfferId, ct);
            if (offer is null) return Error.NotFound($"Offer {command.OfferId} does not exist");

            var names = command.Ingredients.Select(n => n.Trim()).Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (command.Keyword is not null && names.Count != 1)
                return Error.Invalid("A keyword can only be saved when mapping to exactly one ingredient");

            var resolved = await ResolveIngredientsAsync(names, command.Category, householdId, ct);
            if (!resolved.IsSuccess) return resolved.Error!;
            var (ingredients, created) = resolved.Value!;

            offer.MapManually([.. ingredients.Select(i => i.Id)]);

            string? keywordSaved = null;
            if (ingredients.Count == 1)
                keywordSaved = await SaveKeywordAsync(ingredients[0], command.Keyword ?? DefaultKeyword(offer.Heading), ct);

            await db.SaveChangesAsync(ct);
            var rematch = keywordSaved is null ? null : await OfferRematcher.RematchAllAsync(db, ct);

            return new Response(offer.Id, offer.Heading, [.. ingredients.Select(i => i.Name)], keywordSaved,
                [.. created.Select(i => i.Name)], rematch?.Changed ?? 0);
        }

        private async Task<Result<(List<Ingredient> Ingredients, List<Ingredient> Created)>> ResolveIngredientsAsync(
            List<string> names, string? categoryName, int householdId, CancellationToken ct)
        {
            var known = await db.Ingredients
                .Where(i => i.HouseholdId == null || i.HouseholdId == householdId)
                .ToListAsync(ct);

            var result = new List<Ingredient>();
            var created = new List<Ingredient>();
            foreach (var name in names)
            {
                var ingredient = known.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
                if (ingredient is null)
                {
                    if (string.IsNullOrWhiteSpace(categoryName))
                    {
                        var suggestions = known
                            .Select(i => (i.Name, Score: FuzzySearch.Score(name, i.Name)))
                            .Where(s => s.Score > 0).OrderByDescending(s => s.Score).Take(5).Select(s => s.Name).ToList();
                        return Error.Invalid($"Unknown ingredient '{name}'. " + (suggestions.Count > 0
                            ? $"Did you mean: {string.Join(", ", suggestions)}? "
                            : "") + "Pass a category to create it.");
                    }

                    var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == categoryName.Trim(), ct);
                    if (category is null)
                    {
                        var all = await db.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name).ToListAsync(ct);
                        return Error.Invalid($"Unknown category '{categoryName}'. Categories: {string.Join(", ", all)}");
                    }

                    // Offers are shared reference data, so ingredients created from an offer are shared too.
                    ingredient = new Ingredient { Name = name, Category = category, Aliases = [new IngredientAlias { Text = name.ToLowerInvariant() }] };
                    db.Ingredients.Add(ingredient);
                    known.Add(ingredient);
                    created.Add(ingredient);
                }
                result.Add(ingredient);
            }

            if (created.Count > 0) await db.SaveChangesAsync(ct);
            return (result, created);
        }

        private async Task<string?> SaveKeywordAsync(Ingredient ingredient, string keyword, CancellationToken ct)
        {
            var text = IngredientMatcher.Normalize(keyword);
            if (text.Length == 0) return null;

            var exists = await db.IngredientAliases.AnyAsync(a =>
                a.IngredientId == ingredient.Id && a.HouseholdId == null && !a.IsExclusion && a.Text == text, ct);
            if (!exists)
                db.IngredientAliases.Add(new IngredientAlias { IngredientId = ingredient.Id, Text = text });
            return text;
        }

        // The matcher splits headings on "," / "eller" / "el.", so a keyword containing them would never match.
        private static string DefaultKeyword(string heading) =>
            IngredientMatcher.Normalize(heading).Split([",", " eller ", " el. ", "/"], StringSplitOptions.TrimEntries)[0];
    }
}
