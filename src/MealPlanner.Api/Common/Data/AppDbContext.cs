using MealPlanner.Api.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MealPlanner.Api.Common.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<User>(options), IDataProtectionKeyContext
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<HouseholdStore> HouseholdStores => Set<HouseholdStore>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryRule> CategoryRules => Set<CategoryRule>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientAlias> IngredientAliases => Set<IngredientAlias>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<OfferIngredient> OfferIngredients => Set<OfferIngredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<ItemStorePreference> ItemStorePreferences => Set<ItemStorePreference>();
    public DbSet<PurchaseHistoryEntry> PurchaseHistory => Set<PurchaseHistoryEntry>();
    public DbSet<PinnedItem> PinnedItems => Set<PinnedItem>();
    public DbSet<ItemSnooze> ItemSnoozes => Set<ItemSnooze>();
    public DbSet<ShoppingTrip> ShoppingTrips => Set<ShoppingTrip>();
    public DbSet<ShoppingTripStore> ShoppingTripStores => Set<ShoppingTripStore>();
    public DbSet<ShoppingTripItem> ShoppingTripItems => Set<ShoppingTripItem>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Reference data and users are never deleted through a child, so only true children cascade.
        // This also avoids SQL Server's "multiple cascade paths" error.
        Type[] noCascadeFrom = [typeof(Household), typeof(Store), typeof(Category), typeof(User)];
        var foreignKeys = modelBuilder.Model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == typeof(Household).Namespace)
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => noCascadeFrom.Contains(fk.PrincipalEntityType.ClrType));
        foreach (var fk in foreignKeys)
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
