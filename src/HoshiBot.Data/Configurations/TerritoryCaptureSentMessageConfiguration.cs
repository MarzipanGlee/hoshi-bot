using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class TerritoryCaptureSentMessageConfiguration : IEntityTypeConfiguration<TerritoryCaptureSentMessage>
{
    public void Configure(EntityTypeBuilder<TerritoryCaptureSentMessage> builder)
    {
        builder.HasKey(m => m.Id);

        // The DedupKey encodes kind + alliance link + the capture/week it belongs to, so a unique
        // index on (GuildAllianceId, DedupKey) makes every post idempotent: a 5-minute reminder tick
        // can't double-send a Single, and a cron misfire-replay can't post the same digest twice.
        builder.HasIndex(m => new { m.GuildAllianceId, m.DedupKey }).IsUnique();
    }
}
