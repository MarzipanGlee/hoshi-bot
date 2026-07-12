using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcEventDateConfirmationConfiguration : IEntityTypeConfiguration<StfcEventDateConfirmation>
{
    public void Configure(EntityTypeBuilder<StfcEventDateConfirmation> builder)
    {
        builder.HasIndex(c => new { c.StfcNewsPostId, c.DiscordUserId }).IsUnique();

        builder.HasOne(c => c.StfcNewsPost)
            .WithMany(p => p.Confirmations)
            .HasForeignKey(c => c.StfcNewsPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
