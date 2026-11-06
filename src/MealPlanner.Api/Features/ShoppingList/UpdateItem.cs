using System.Security.Claims;
using MealPlanner.Api.Common.Auth;
using MealPlanner.Api.Common.Categories;
using MealPlanner.Api.Common.Data;
using MealPlanner.Api.Common.Lists;
using MealPlanner.Api.Common.Matching;
using MealPlanner.Api.Common.Offers;
using MealPlanner.Api.Common.Realtime;
using MealPlanner.Api.Common.Results;
using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Features.ShoppingList;

/// <summary>
/// Corrects an item ("det er rigtigt smør", "havarti er Castello") and recalculates its offers (spec 12.5).
/// Moving it to another category saves a learned rule; RememberStore saves a standing store choice.
/// PATCH /api/shopping-list/items/{id} and update_shopping_list_item.
/// </summary>
public static class UpdateItem
{
    /// <summary>Null leaves a field unchanged; an empty string clears quantity, variant or tag.</summary>
    public sealed record Command(int ItemId, string? Name = null, string? Quantity = null, string? Variant = null,
        string? Tag = null, int? CategoryId = null, int? FlaggedStoreId = null, bool ClearFlag = false, bool RememberStore = false);

    public sealed record Body(string? Name = null, string? Quantity = null, string? Variant = null, string? Tag = null,
        int? CategoryId = null, int? FlaggedStoreId = null, bool ClearFlag = false, bool RememberStore = false);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPatch("/api/shopping-list/items/{id:int}", async (int id, Body b, ClaimsPrincipal user, Handler handler,
                CancellationToken ct) =>
            (await handler.HandleAsync(new Command(id, b.Name, b.Quantity, b.Variant, b.Tag, b.CategoryId, b.FlaggedStoreId,
                b.ClearFlag, b.RememberStore), user.GetHouseholdId(), ct)).ToHttpResult());

    public sealed class Handler(AppDbContext db, TimeProvider clock, ListNotifier notifier)
    {
        public async Task<Result<ShoppingListViews.ItemDto>> HandleAsync(Command c, int householdId, CancellationToken ct)
        {
            var item = await db.ShoppingListItems
                .SingleOrDefaultAsync(i => i.Id == c.ItemId && i.ShoppingList.HouseholdId == householdId, ct);
            if (item is null) return Error.NotFound($"Item {c.ItemId} is not on the shopping list");

            var stores = await ShoppingListViews.LoadStoresAsync(db, householdId, ct);

            if (!string.IsNullOrWhiteSpace(c.Name) && c.Name.Trim() != item.Name)
            {
                item.Name = c.Name.Trim();
                var classification = (await CategoryClassifier.LoadAsync(db, householdId, ct)).Classify(item.Name);
                item.IngredientId = classification.IngredientId;
                if (c.CategoryId is null) item.CategoryId = classification.CategoryId;
            }
            if (c.Quantity is not null) item.Quantity = Blank(c.Quantity);
            if (c.Variant is not null) item.Variant = Blank(c.Variant);
            if (c.Tag is not null) item.Tag = Blank(c.Tag);

            if (c.CategoryId is { } categoryId && categoryId != item.CategoryId)
            {
                if (!await db.Categories.AnyAsync(cat => cat.Id == categoryId, ct))
                    return Error.Invalid($"Category {categoryId} does not exist");
                item.CategoryId = categoryId;
                await LearnCategoryAsync(householdId, item.Name, categoryId, ct);
            }

            if (c.ClearFlag)
            {
                item.ClearFlag();
            }
            else if (c.FlaggedStoreId is { } storeId)
            {
                if (stores.All(s => s.Id != storeId)) return Error.Invalid($"Store {storeId} is not one of the household's stores");
                item.FlagTo(storeId, StoreFlagSource.Manual);
            }
            if (c.RememberStore) await RememberStoreAsync(householdId, item, ct);

            await db.SaveChangesAsync(ct);
            await notifier.ListChangedAsync(householdId, ct);

            var offers = await OfferLookup.LoadAsync(db, householdId, DanishTime.Today(clock), ct);
            var ingredientNames = await ShoppingListViews.LoadIngredientNamesAsync(db, [item], ct);
            return ShoppingListViews.ToDto(item, offers, stores.ToDictionary(s => s.Id), ingredientNames);
        }

        private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>The user moved the item: remember it for this household (spec 4, "lært regel").</summary>
        private async Task LearnCategoryAsync(int householdId, string name, int categoryId, CancellationToken ct)
        {
            var term = IngredientMatcher.Normalize(name);
            var rule = await db.CategoryRules.FirstOrDefaultAsync(r => r.HouseholdId == householdId && r.Term == term, ct);
            if (rule is null)
                db.CategoryRules.Add(new CategoryRule { Term = term, CategoryId = categoryId, HouseholdId = householdId, IsLearned = true });
            else
                rule.CategoryId = categoryId;
        }

        /// <summary>"Bleer køber vi altid i Netto": the current flag becomes the standing choice; no flag removes it.</summary>
        private async Task RememberStoreAsync(int householdId, ShoppingListItem item, CancellationToken ct)
        {
            var name = IngredientMatcher.Normalize(item.Name);
            var preference = await db.ItemStorePreferences.FirstOrDefaultAsync(p => p.HouseholdId == householdId && p.ItemName == name, ct);
            if (item.FlaggedStoreId is not { } storeId)
            {
                if (preference is not null) db.ItemStorePreferences.Remove(preference);
            }
            else if (preference is null)
            {
                db.ItemStorePreferences.Add(new ItemStorePreference { HouseholdId = householdId, ItemName = name, StoreId = storeId });
            }
            else
            {
                preference.StoreId = storeId;
            }
        }
    }
}
