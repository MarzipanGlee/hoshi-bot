using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.GuildId);

        builder.HasOne(a => a.Guild)
            .WithMany()
            .HasForeignKey(a => a.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.StfcSystem)
            .WithMany()
            .HasForeignKey(a => a.StfcSystemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
