using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildFeatureSettingSnowflakeConfiguration : IEntityTypeConfiguration<GuildFeatureSettingSnowflake>
{
    public void Configure(EntityTypeBuilder<GuildFeatureSettingSnowflake> builder)
    {
        builder.HasKey(s => s.Id);

        // Stops a literal duplicate list entry (e.g. the same channel added twice under one
        // key) while still allowing many different values under the same key for list
        // settings. Singularity for single-value keys is enforced by
        // GuildFeatureSettingsService (upsert-by-replace), not by this constraint.
        builder.HasIndex(s => new { s.GuildId, s.Feature, s.Audience, s.Key, s.Value }).IsUnique();

        // Lookup performance for the list case, where the unique index above doesn't help
        // narrow to the relevant rows.
        builder.HasIndex(s => new { s.GuildId, s.Feature, s.Audience, s.Key });

        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
