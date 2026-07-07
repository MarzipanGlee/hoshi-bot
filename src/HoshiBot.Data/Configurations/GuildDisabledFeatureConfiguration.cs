using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildDisabledFeatureConfiguration : IEntityTypeConfiguration<GuildDisabledFeature>
{
    public void Configure(EntityTypeBuilder<GuildDisabledFeature> builder)
    {
        builder.HasKey(f => f.Id);
        builder.HasIndex(f => new { f.GuildId, f.Feature }).IsUnique();

        builder.HasOne(f => f.Guild)
            .WithMany()
            .HasForeignKey(f => f.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
