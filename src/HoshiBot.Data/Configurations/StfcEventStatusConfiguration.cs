using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcEventStatusConfiguration : IEntityTypeConfiguration<StfcEventStatus>
{
    public void Configure(EntityTypeBuilder<StfcEventStatus> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventGroup).HasMaxLength(50);

        // EventGroup alone used to be the PK; now a group can have several rows (one per
        // region, for "incursions"), so uniqueness moves to the (EventGroup, RegionId) pair.
        builder.HasIndex(e => new { e.EventGroup, e.RegionId }).IsUnique();

        builder.HasOne(e => e.Region)
            .WithMany()
            .HasForeignKey(e => e.RegionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
