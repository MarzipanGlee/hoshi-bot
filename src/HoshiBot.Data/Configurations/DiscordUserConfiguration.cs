using HoshiBot.Domain;
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

        // Short by design: it's appended to a nickname that already spends up to ~14 characters on
        // tags inside Discord's 32-char limit. NicknameComposer drops it entirely when it doesn't
        // fit, so a long one would just never show.
        builder.Property(u => u.NicknameSuffix).HasMaxLength(NicknameComposer.MaxSuffixLength);
    }
}
