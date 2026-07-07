using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class DiscordGuildConfiguration : IEntityTypeConfiguration<DiscordGuild>
{
    public void Configure(EntityTypeBuilder<DiscordGuild> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Locale).HasMaxLength(10).IsRequired();
    }
}
