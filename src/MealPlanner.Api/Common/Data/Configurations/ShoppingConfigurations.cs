using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealPlanner.Api.Common.Data.Configurations;

public class ShoppingListConfiguration : IEntityTypeConfiguration<ShoppingList>
{
    public void Configure(EntityTypeBuilder<ShoppingList> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        // One active list per household.
        b.HasIndex(x => x.HouseholdId).IsUnique().HasFilter("[IsActive] = 1");
    }
}

public class ShoppingListItemConfiguration : IEntityTypeConfiguration<ShoppingListItem>
{
    public void Configure(EntityTypeBuilder<ShoppingListItem> b)
    {
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Quantity).HasMaxLength(100);
        b.Property(x => x.Variant).HasMaxLength(200);
        b.Property(x => x.Tag).HasMaxLength(100);
        b.Property(x => x.FlagSource).HasConversion<string>().HasMaxLength(20);

        b.HasOne(x => x.PlannedStore).WithMany().HasForeignKey(x => x.PlannedStoreId);
        b.HasOne(x => x.FlaggedStore).WithMany().HasForeignKey(x => x.FlaggedStoreId);
        b.HasOne(x => x.Offer).WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.SetNull);
    }
}
