using MealPlanner.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MealPlanner.Api.Common.Data.Configurations;

public class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> b)
    {
        b.Property(x => x.TjekId).HasMaxLength(50);
        b.Property(x => x.Heading).HasMaxLength(500);
        b.HasIndex(x => x.TjekId).IsUnique();
        b.HasIndex(x => new { x.StoreId, x.ValidTo });
    }
}

public class OfferIngredientConfiguration : IEntityTypeConfiguration<OfferIngredient>
{
    public void Configure(EntityTypeBuilder<OfferIngredient> b)
    {
        b.HasKey(x => new { x.OfferId, x.IngredientId });
        b.HasOne(x => x.Offer).WithMany(o => o.Ingredients).HasForeignKey(x => x.OfferId);
    }
}
