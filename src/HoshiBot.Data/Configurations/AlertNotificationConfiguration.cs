using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class AlertNotificationConfiguration : IEntityTypeConfiguration<AlertNotification>
{
    public void Configure(EntityTypeBuilder<AlertNotification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.HasIndex(n => n.AlertId);

        builder.HasOne(n => n.Alert)
            .WithMany(a => a.Notifications)
            .HasForeignKey(n => n.AlertId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
