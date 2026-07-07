using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcRegionConfiguration : IEntityTypeConfiguration<StfcRegion>
{
    public void Configure(EntityTypeBuilder<StfcRegion> builder)
    {
        builder.HasKey(r => r.Id);
        // Ids match Scopely's own region numbering (US=1, EU=2, APAC=3), not an EF sequence —
        // assigned explicitly by the seeder/admin, never auto-generated.
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.Name).IsUnique();
    }
}
