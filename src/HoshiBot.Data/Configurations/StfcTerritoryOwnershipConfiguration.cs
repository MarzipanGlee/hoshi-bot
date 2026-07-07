using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcTerritoryOwnershipConfiguration : IEntityTypeConfiguration<StfcTerritoryOwnership>
{
    public void Configure(EntityTypeBuilder<StfcTerritoryOwnership> builder)
    {
        builder.HasKey(o => o.Id);

        // Uniqueness on (TerritoryId, ServerId) is enforced in application code, not
        // here — see the entity's comment for why. Non-unique index for lookup speed.
        builder.HasIndex(o => new { o.TerritoryId, o.ServerId });

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
