using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class NotificationRoleConfiguration : IEntityTypeConfiguration<NotificationRole>
{
    public void Configure(EntityTypeBuilder<NotificationRole> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.GuildId, r.Kind }).IsUnique();

        builder.HasOne(r => r.Guild)
            .WithMany(g => g.NotificationRoles)
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
