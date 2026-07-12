using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class IncursionsRegionDefaultConfiguration : IEntityTypeConfiguration<IncursionsRegionDefault>
{
    public void Configure(EntityTypeBuilder<IncursionsRegionDefault> builder)
    {
        builder.HasIndex(d => d.RegionId).IsUnique();

        builder.HasOne(d => d.Region)
            .WithMany()
            .HasForeignKey(d => d.RegionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
