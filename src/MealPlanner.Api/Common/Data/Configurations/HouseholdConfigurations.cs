using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealPlanner.Api.Common.Data.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.DisplayName).HasMaxLength(100);
    }
}

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.KeyHash).HasMaxLength(100);
        b.HasIndex(x => x.KeyHash).IsUnique();
    }
}

public class HouseholdStoreConfiguration : IEntityTypeConfiguration<HouseholdStore>
{
    public void Configure(EntityTypeBuilder<HouseholdStore> b)
    {
        b.Property(x => x.StopGroup).HasMaxLength(50);
        b.HasIndex(x => new { x.HouseholdId, x.StoreId }).IsUnique();
    }
}

public class ItemStorePreferenceConfiguration : IEntityTypeConfiguration<ItemStorePreference>
{
    public void Configure(EntityTypeBuilder<ItemStorePreference> b)
    {
        b.Property(x => x.ItemName).HasMaxLength(200);
        b.HasIndex(x => new { x.HouseholdId, x.ItemName }).IsUnique();
    }
}

public class PurchaseHistoryEntryConfiguration : IEntityTypeConfiguration<PurchaseHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PurchaseHistoryEntry> b)
    {
        b.ToTable("PurchaseHistory");
        b.Property(x => x.ItemName).HasMaxLength(200);
        b.Property(x => x.Quantity).HasMaxLength(100);
        b.HasIndex(x => new { x.HouseholdId, x.ItemName, x.BoughtAt });
    }
}

public class ItemSnoozeConfiguration : IEntityTypeConfiguration<ItemSnooze>
{
    public void Configure(EntityTypeBuilder<ItemSnooze> b)
    {
        b.Property(x => x.ItemName).HasMaxLength(200);
        b.HasIndex(x => new { x.HouseholdId, x.ItemName }).IsUnique();
    }
}

public class PinnedItemConfiguration : IEntityTypeConfiguration<PinnedItem>
{
    public void Configure(EntityTypeBuilder<PinnedItem> b)
    {
        b.Property(x => x.ItemName).HasMaxLength(200);
        b.Property(x => x.DefaultQuantity).HasMaxLength(100);
        b.HasIndex(x => new { x.HouseholdId, x.ItemName }).IsUnique();
    }
}
