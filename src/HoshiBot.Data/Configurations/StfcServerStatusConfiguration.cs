using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcServerStatusConfiguration : IEntityTypeConfiguration<StfcServerStatus>
{
    public void Configure(EntityTypeBuilder<StfcServerStatus> builder)
    {
        builder.HasKey(s => s.StfcServerId);
        builder.Property(s => s.Maintenance).HasMaxLength(50).IsRequired();
        builder.Property(s => s.NotifiedMaintenance).HasMaxLength(50);

        builder.HasOne(s => s.StfcServer)
            .WithOne()
            .HasForeignKey<StfcServerStatus>(s => s.StfcServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
