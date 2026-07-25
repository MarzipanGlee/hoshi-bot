using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class TerritoryServiceSelectionConfiguration : IEntityTypeConfiguration<TerritoryServiceSelection>
{
    public void Configure(EntityTypeBuilder<TerritoryServiceSelection> builder)
    {
        builder.HasKey(s => s.Id);

        // A service is in at most one priority per zone per alliance; the reminder looks up by
        // (alliance, territory).
        builder.HasIndex(s => new { s.GuildAllianceId, s.TerritoryId, s.ServiceId }).IsUnique();
        builder.HasIndex(s => new { s.GuildAllianceId, s.TerritoryId });

        // Cascade from the alliance link (the single cascade path — unlinking an alliance removes
        // its selections). Territory/Service are Restrict to avoid multiple cascade paths (Postgres
        // rejects them); the sync never deletes a territory/service still referenced here.
        builder.HasOne(s => s.GuildAlliance)
            .WithMany()
            .HasForeignKey(s => s.GuildAllianceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Territory)
            .WithMany()
            .HasForeignKey(s => s.TerritoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Service)
            .WithMany()
            .HasForeignKey(s => s.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
