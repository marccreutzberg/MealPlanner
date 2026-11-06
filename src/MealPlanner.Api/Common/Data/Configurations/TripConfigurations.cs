using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealPlanner.Api.Common.Data.Configurations;

public class ShoppingTripConfiguration : IEntityTypeConfiguration<ShoppingTrip>
{
    public void Configure(EntityTypeBuilder<ShoppingTrip> b)
    {
        // At most one active trip per household.
        b.HasIndex(x => x.HouseholdId).IsUnique().HasFilter("[FinishedAt] IS NULL");
        b.HasMany(x => x.Stores).WithOne(x => x.Trip).HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Items).WithOne(x => x.Trip).HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShoppingTripStoreConfiguration : IEntityTypeConfiguration<ShoppingTripStore>
{
    public void Configure(EntityTypeBuilder<ShoppingTripStore> b) => b.HasKey(x => new { x.TripId, x.StoreId });
}

public class ShoppingTripItemConfiguration : IEntityTypeConfiguration<ShoppingTripItem>
{
    public void Configure(EntityTypeBuilder<ShoppingTripItem> b)
    {
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Quantity).HasMaxLength(100);
        b.Property(x => x.Variant).HasMaxLength(200);
        b.Property(x => x.Tag).HasMaxLength(100);
        b.Property(x => x.OfferHeading).HasMaxLength(300);
        b.Property(x => x.PriceLabel).HasMaxLength(50);
        // No database cascade: TripFreezer copies the item and clears the link before list items are deleted.
        // A SET NULL rule here would give SQL Server more than one cascade path.
        b.HasOne(x => x.ShoppingListItem).WithMany().HasForeignKey(x => x.ShoppingListItemId)
            .OnDelete(DeleteBehavior.ClientSetNull);
        // A list item is on a trip at most once, also when two phones take new items along at the same time.
        b.HasIndex(x => new { x.TripId, x.ShoppingListItemId }).IsUnique().HasFilter("[ShoppingListItemId] IS NOT NULL");
    }
}
