using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealPlanner.Api.Common.Data.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.TjekDealerId).HasMaxLength(20);
        b.Property(x => x.Color).HasMaxLength(9);
        b.HasIndex(x => x.TjekDealerId).IsUnique();
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Color).HasMaxLength(9);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class CategoryRuleConfiguration : IEntityTypeConfiguration<CategoryRule>
{
    public void Configure(EntityTypeBuilder<CategoryRule> b)
    {
        b.Property(x => x.Term).HasMaxLength(200);
        // No filter: one shared rule (HouseholdId null) per term, plus one per household.
        b.HasIndex(x => new { x.Term, x.HouseholdId }).IsUnique().HasFilter(null);
    }
}

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> b)
    {
        b.Property(x => x.Name).HasMaxLength(200);
        b.HasIndex(x => new { x.Name, x.HouseholdId }).IsUnique().HasFilter(null);
    }
}

public class IngredientAliasConfiguration : IEntityTypeConfiguration<IngredientAlias>
{
    public void Configure(EntityTypeBuilder<IngredientAlias> b)
    {
        b.Property(x => x.Text).HasMaxLength(200);
    }
}
