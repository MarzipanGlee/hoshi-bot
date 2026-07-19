using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildMemberNoteConfiguration : IEntityTypeConfiguration<GuildMemberNote>
{
    public void Configure(EntityTypeBuilder<GuildMemberNote> builder)
    {
        builder.HasKey(n => n.Id);

        // One note per member per guild — the extraction/edit paths upsert on this.
        builder.HasIndex(n => new { n.GuildId, n.DiscordUserId }).IsUnique();

        builder.HasOne(n => n.Guild)
            .WithMany()
            .HasForeignKey(n => n.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
