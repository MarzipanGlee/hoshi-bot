using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildSettingsConfiguration : IEntityTypeConfiguration<GuildSettings>
{
    public void Configure(EntityTypeBuilder<GuildSettings> builder)
    {
        builder.HasKey(s => s.GuildId);
        builder.Property(s => s.GuildId).ValueGeneratedNever();
        builder.Property(s => s.Language).HasMaxLength(10);

        builder.HasOne(s => s.Guild)
            .WithOne()
            .HasForeignKey<GuildSettings>(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
