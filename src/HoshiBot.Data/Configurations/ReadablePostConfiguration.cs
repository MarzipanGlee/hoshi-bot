using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ReadablePostConfiguration : IEntityTypeConfiguration<ReadablePost>
{
    public void Configure(EntityTypeBuilder<ReadablePost> builder)
    {
        builder.HasKey(p => p.Id);

        // One row per posted message. A message id is already unique Discord-wide, but the guild is
        // in the key so the index also serves the per-guild lookups below it.
        builder.HasIndex(p => new { p.GuildId, p.MessageId }).IsUnique();

        // The unread list's query: this guild's tracked posts, oldest first.
        builder.HasIndex(p => new { p.GuildId, p.ReadReceiptsEnabled, p.PostedAt });

        builder.HasOne(p => p.Guild)
            .WithMany()
            .HasForeignKey(p => p.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
