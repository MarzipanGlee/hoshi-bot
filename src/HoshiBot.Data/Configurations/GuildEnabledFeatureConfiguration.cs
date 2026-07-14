using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildEnabledFeatureConfiguration : IEntityTypeConfiguration<GuildEnabledFeature>
{
    public void Configure(EntityTypeBuilder<GuildEnabledFeature> builder)
    {
        builder.HasKey(f => f.Id);

        // GuildAllianceId scopes the Alliance audience per specific alliance; it's null for all
        // other audiences. AreNullsDistinct(false) (PG NULLS NOT DISTINCT) keeps the null case
        // deduplicated so a non-alliance (GuildId, Feature, Audience) still can't be doubled.
        builder.HasIndex(f => new { f.GuildId, f.Feature, f.Audience, f.GuildAllianceId })
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasOne(f => f.Guild)
            .WithMany()
            .HasForeignKey(f => f.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade so unlinking an alliance (deleting its GuildAlliance) removes that alliance's
        // enabled-feature rows too. Multiple cascade paths (Guild directly + via GuildAlliance)
        // are fine on Postgres.
        builder.HasOne(f => f.GuildAlliance)
            .WithMany()
            .HasForeignKey(f => f.GuildAllianceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
