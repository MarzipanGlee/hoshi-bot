using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildFeatureChannelConfiguration : IEntityTypeConfiguration<GuildFeatureChannel>
{
    public void Configure(EntityTypeBuilder<GuildFeatureChannel> builder)
    {
        builder.HasKey(c => c.Id);

        // Same channel can't be added twice for the same (feature, audience) in a guild.
        builder.HasIndex(c => new { c.GuildId, c.Feature, c.Audience, c.ChannelId }).IsUnique();

        builder.HasOne(c => c.Guild)
            .WithMany()
            .HasForeignKey(c => c.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
