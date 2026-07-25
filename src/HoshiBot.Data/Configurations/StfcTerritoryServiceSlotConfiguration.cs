using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcTerritoryServiceSlotConfiguration : IEntityTypeConfiguration<StfcTerritoryServiceSlot>
{
    public void Configure(EntityTypeBuilder<StfcTerritoryServiceSlot> builder)
    {
        builder.HasKey(s => s.Id);

        // One row per (server, territory, service); the reminder looks up by (server, territory).
        builder.HasIndex(s => new { s.ServerId, s.TerritoryId, s.ServiceId }).IsUnique();
        builder.HasIndex(s => new { s.ServerId, s.TerritoryId });

        builder.HasOne(s => s.Server)
            .WithMany()
            .HasForeignKey(s => s.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Territory)
            .WithMany()
            .HasForeignKey(s => s.TerritoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on the service so removing a catalog row doesn't create a second cascade path
        // into this table (Postgres/EF reject multiple cascade paths); the sync replaces slots
        // per server, so orphaned services are cleaned up there, not by cascade.
        builder.HasOne(s => s.Service)
            .WithMany(s => s.Slots)
            .HasForeignKey(s => s.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
