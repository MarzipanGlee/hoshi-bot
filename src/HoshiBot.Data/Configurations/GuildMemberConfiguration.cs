using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildMemberConfiguration : IEntityTypeConfiguration<GuildMember>
{
    public void Configure(EntityTypeBuilder<GuildMember> builder)
    {
        builder.HasKey(gm => new { gm.GuildId, gm.DiscordUserId });

        builder.HasOne(gm => gm.Guild)
            .WithMany(g => g.Members)
            .HasForeignKey(gm => gm.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gm => gm.User)
            .WithMany(u => u.GuildMemberships)
            .HasForeignKey(gm => gm.DiscordUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Cascade: a catalog player being deleted (or re-imported away) must cost the
        // member their per-guild pick, never their membership row.
        builder.HasOne(gm => gm.PrimaryStfcPlayer)
            .WithMany()
            .HasForeignKey(gm => gm.PrimaryStfcPlayerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
