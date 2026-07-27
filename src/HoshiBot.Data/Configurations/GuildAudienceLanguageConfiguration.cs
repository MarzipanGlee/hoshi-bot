using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildAudienceLanguageConfiguration : IEntityTypeConfiguration<GuildAudienceLanguage>
{
    public void Configure(EntityTypeBuilder<GuildAudienceLanguage> builder)
    {
        builder.HasKey(l => new { l.GuildId, l.Audience });
        builder.Property(l => l.Language).HasMaxLength(10).IsRequired();

        builder.HasOne(l => l.Guild)
            .WithMany()
            .HasForeignKey(l => l.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
