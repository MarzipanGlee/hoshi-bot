using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildFeatureSettingTextConfiguration : IEntityTypeConfiguration<GuildFeatureSettingText>
{
    public void Configure(EntityTypeBuilder<GuildFeatureSettingText> builder)
    {
        builder.HasKey(s => s.Id);

        // No Value in the index — free text isn't a sensible index key, and every current
        // text setting is singular anyway (enforced by GuildFeatureSettingsService's
        // upsert-by-replace, same as the snowflake table).
        builder.HasIndex(s => new { s.GuildId, s.Feature, s.Audience, s.Key, s.GuildAllianceId })
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade so unlinking an alliance removes its per-alliance settings. Multiple cascade
        // paths (Guild directly + via GuildAlliance) are fine on Postgres.
        builder.HasOne(s => s.GuildAlliance)
            .WithMany()
            .HasForeignKey(s => s.GuildAllianceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
