using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealPlanner.Api.Common.Data.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> b)
    {
        b.Property(x => x.Name).HasMaxLength(200);
    }
}

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> b)
    {
        b.Property(x => x.Text).HasMaxLength(300);
        b.Property(x => x.Quantity).HasMaxLength(100);
        b.HasOne(x => x.Recipe).WithMany(r => r.Ingredients).HasForeignKey(x => x.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MealPlanConfiguration : IEntityTypeConfiguration<MealPlan>
{
    public void Configure(EntityTypeBuilder<MealPlan> b)
    {
        b.HasIndex(x => new { x.HouseholdId, x.WeekStart }).IsUnique();
    }
}

public class MealPlanEntryConfiguration : IEntityTypeConfiguration<MealPlanEntry>
{
    public void Configure(EntityTypeBuilder<MealPlanEntry> b)
    {
        b.HasIndex(x => new { x.MealPlanId, x.Date }).IsUnique();
        // A recipe in use by a meal plan cannot be deleted silently.
        b.HasOne(x => x.Recipe).WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Restrict);
    }
}
