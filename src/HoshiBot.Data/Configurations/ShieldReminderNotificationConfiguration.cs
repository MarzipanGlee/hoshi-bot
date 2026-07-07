using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ShieldReminderNotificationConfiguration : IEntityTypeConfiguration<ShieldReminderNotification>
{
    public void Configure(EntityTypeBuilder<ShieldReminderNotification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.HasIndex(n => n.ShieldReminderId);

        builder.HasOne(n => n.ShieldReminder)
            .WithMany(s => s.Notifications)
            .HasForeignKey(n => n.ShieldReminderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
