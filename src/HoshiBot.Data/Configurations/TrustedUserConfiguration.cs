using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class TrustedUserConfiguration : IEntityTypeConfiguration<TrustedUser>
{
    public void Configure(EntityTypeBuilder<TrustedUser> builder)
    {
        builder.HasIndex(u => u.DiscordUserId).IsUnique();
    }
}
