using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class DiscordUserConfiguration : IEntityTypeConfiguration<DiscordUser>
{
    public void Configure(EntityTypeBuilder<DiscordUser> builder)
    {
        builder.HasKey(u => u.DiscordUserId);
        builder.Property(u => u.DiscordUserId).ValueGeneratedNever();
        builder.Property(u => u.Language).HasMaxLength(10);
        builder.Property(u => u.DiscordLocale).HasMaxLength(10);
    }
}
