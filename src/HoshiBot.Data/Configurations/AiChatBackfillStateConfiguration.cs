using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class AiChatBackfillStateConfiguration : IEntityTypeConfiguration<AiChatBackfillState>
{
    public void Configure(EntityTypeBuilder<AiChatBackfillState> builder)
    {
        builder.HasKey(s => s.Id);

        // One cursor per (guild, channel) — the upsert key.
        builder.HasIndex(s => new { s.GuildId, s.ChannelId }).IsUnique();

        builder.HasIndex(s => s.GuildId);

        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
