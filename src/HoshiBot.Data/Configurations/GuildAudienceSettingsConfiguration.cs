using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildAudienceSettingsConfiguration : IEntityTypeConfiguration<GuildAudienceSettings>
{
    public void Configure(EntityTypeBuilder<GuildAudienceSettings> builder)
    {
        builder.HasKey(a => new { a.GuildId, a.Audience });

        builder.Property(a => a.Language).HasMaxLength(8);

        builder.HasOne(a => a.Guild)
            .WithMany()
            .HasForeignKey(a => a.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
