using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ReadReceiptConfiguration : IEntityTypeConfiguration<ReadReceipt>
{
    public void Configure(EntityTypeBuilder<ReadReceipt> builder)
    {
        builder.HasKey(r => r.Id);

        // What makes a double click a no-op rather than a second receipt.
        builder.HasIndex(r => new { r.ReadablePostId, r.DiscordUserId }).IsUnique();

        builder.HasOne(r => r.ReadablePost)
            .WithMany(p => p.Receipts)
            .HasForeignKey(r => r.ReadablePostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.GuildMember)
            .WithMany()
            .HasForeignKey(r => new { r.GuildId, r.DiscordUserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
