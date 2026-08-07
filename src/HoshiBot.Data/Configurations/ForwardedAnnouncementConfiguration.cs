using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ForwardedAnnouncementConfiguration : IEntityTypeConfiguration<ForwardedAnnouncement>
{
    public void Configure(EntityTypeBuilder<ForwardedAnnouncement> builder)
    {
        builder.HasKey(f => f.Id);

        // One row per (source message, destination). Unique on SourceMessageId alone until the
        // forwarder became audience-scoped, which made "the same source, forwarded into a second
        // alliance's channel" a legitimate second row rather than a duplicate.
        builder.HasIndex(f => new { f.SourceMessageId, f.DestinationChannelId }).IsUnique();

        // The catch-up job and the edit path both ask "has this source been forwarded anywhere yet".
        builder.HasIndex(f => f.SourceMessageId);

        builder.HasOne(f => f.Guild)
            .WithMany()
            .HasForeignKey(f => f.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
