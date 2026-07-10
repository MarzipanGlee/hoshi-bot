using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildEnabledFeatureConfiguration : IEntityTypeConfiguration<GuildEnabledFeature>
{
    public void Configure(EntityTypeBuilder<GuildEnabledFeature> builder)
    {
        builder.HasKey(f => f.Id);
        builder.HasIndex(f => new { f.GuildId, f.Feature, f.Audience }).IsUnique();

        builder.HasOne(f => f.Guild)
            .WithMany()
            .HasForeignKey(f => f.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
