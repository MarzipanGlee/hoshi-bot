using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GlobalAdminConfiguration : IEntityTypeConfiguration<GlobalAdmin>
{
    public void Configure(EntityTypeBuilder<GlobalAdmin> builder)
    {
        builder.HasKey(a => a.DiscordUserId);
        builder.Property(a => a.DiscordUserId).ValueGeneratedNever();
    }
}
