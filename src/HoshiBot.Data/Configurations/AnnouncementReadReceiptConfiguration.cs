using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class AnnouncementReadReceiptConfiguration : IEntityTypeConfiguration<AnnouncementReadReceipt>
{
    public void Configure(EntityTypeBuilder<AnnouncementReadReceipt> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.AnnouncementId, r.GuildId, r.DiscordUserId }).IsUnique();

        builder.HasOne(r => r.GuildMember)
            .WithMany()
            .HasForeignKey(r => new { r.GuildId, r.DiscordUserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
