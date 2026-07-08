using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcAllianceConfiguration : IEntityTypeConfiguration<StfcAlliance>
{
    public void Configure(EntityTypeBuilder<StfcAlliance> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Tag).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();

        // ExternalId, not (ServerId, Tag) — a Tag is just an alliance's latest known tag
        // and can legitimately change (see NameHistory), so it can't be part of a
        // uniqueness constraint; ExternalId is the stable identity instead.
        builder.HasIndex(a => a.ExternalId).IsUnique();

        builder.HasOne(a => a.Server)
            .WithMany(s => s.Alliances)
            .HasForeignKey(a => a.ServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
