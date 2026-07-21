using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ForwardedAnnouncementConfiguration : IEntityTypeConfiguration<ForwardedAnnouncement>
{
    public void Configure(EntityTypeBuilder<ForwardedAnnouncement> builder)
    {
        builder.HasKey(f => f.Id);

        // One tracking row per source message — the live path and the catch-up job both key off this.
        builder.HasIndex(f => f.SourceMessageId).IsUnique();

        builder.HasOne(f => f.Guild)
            .WithMany()
            .HasForeignKey(f => f.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
