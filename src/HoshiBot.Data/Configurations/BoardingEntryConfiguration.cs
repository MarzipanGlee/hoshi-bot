using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class BoardingEntryConfiguration : IEntityTypeConfiguration<BoardingEntry>
{
    public void Configure(EntityTypeBuilder<BoardingEntry> builder)
    {
        builder.HasKey(e => e.Id);

        // One boarding per member per guild — the database half of "most specific scope wins". Two
        // enabled scopes both claiming the same member is a real configuration (an alliance inside a
        // community Discord), and this is what stops it becoming two roles and two DMs.
        builder.HasIndex(e => new { e.GuildId, e.DiscordUserId }).IsUnique();

        builder.HasOne(e => e.Guild)
            .WithMany()
            .HasForeignKey(e => e.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ReadablePost)
            .WithMany()
            .HasForeignKey(e => e.ReadablePostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
