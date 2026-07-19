using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class PlayerLinkReviewConfiguration : IEntityTypeConfiguration<PlayerLinkReview>
{
    public void Configure(EntityTypeBuilder<PlayerLinkReview> builder)
    {
        builder.HasKey(r => r.Id);

        // One review per member per guild — the matcher (join handler + backfill job) dedups on this.
        builder.HasIndex(r => new { r.GuildId, r.DiscordUserId }).IsUnique();

        builder.HasOne(r => r.Guild)
            .WithMany()
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // The best-guess player is optional and must not vanish the review if the player row is
        // pruned by a re-import — null it out instead of cascading.
        builder.HasOne(r => r.CandidateStfcPlayer)
            .WithMany()
            .HasForeignKey(r => r.CandidateStfcPlayerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
