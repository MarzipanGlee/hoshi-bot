using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ShieldReminderConfiguration : IEntityTypeConfiguration<ShieldReminder>
{
    public void Configure(EntityTypeBuilder<ShieldReminder> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.GuildId, s.DiscordUserId }).IsUnique();

        builder.HasOne(s => s.GuildMember)
            .WithMany()
            .HasForeignKey(s => new { s.GuildId, s.DiscordUserId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.StfcSystem)
            .WithMany()
            .HasForeignKey(s => s.StfcSystemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
