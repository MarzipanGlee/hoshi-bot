using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildAdminRoleConfiguration : IEntityTypeConfiguration<GuildAdminRole>
{
    public void Configure(EntityTypeBuilder<GuildAdminRole> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.GuildId, r.DiscordRoleId }).IsUnique();

        builder.HasOne(r => r.Guild)
            .WithMany(g => g.AdminRoles)
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
