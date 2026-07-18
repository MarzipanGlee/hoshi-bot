using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcTerritoryOwnershipConfiguration : IEntityTypeConfiguration<StfcTerritoryOwnership>
{
    public void Configure(EntityTypeBuilder<StfcTerritoryOwnership> builder)
    {
        builder.HasKey(o => o.Id);

        // A territory on a server has exactly one owner, so this pair is unique. Enforced at the
        // DB level (not just in app code) after concurrent Host/Web seeding silently doubled every
        // row — see the entity comment. Both columns are non-null, so a plain (not filtered)
        // unique index is enough.
        builder.HasIndex(o => new { o.TerritoryId, o.ServerId }).IsUnique();

        builder.HasOne(o => o.Territory)
            .WithMany(t => t.Ownerships)
            .HasForeignKey(o => o.TerritoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Server)
            .WithMany()
            .HasForeignKey(o => o.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Alliance)
            .WithMany(a => a.TerritoryOwnerships)
            .HasForeignKey(o => o.AllianceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
