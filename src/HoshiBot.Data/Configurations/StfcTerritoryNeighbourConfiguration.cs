using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcTerritoryNeighbourConfiguration : IEntityTypeConfiguration<StfcTerritoryNeighbour>
{
    public void Configure(EntityTypeBuilder<StfcTerritoryNeighbour> builder)
    {
        builder.HasKey(n => n.Id);
        builder.HasIndex(n => new { n.TerritoryId, n.NeighbourTerritoryId }).IsUnique();

        builder.HasOne(n => n.Territory)
            .WithMany()
            .HasForeignKey(n => n.TerritoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Two cascade paths into StfcTerritories would trip Postgres/SQLite's
        // multiple-cascade-path restriction — this side is Restrict instead.
        builder.HasOne(n => n.NeighbourTerritory)
            .WithMany()
            .HasForeignKey(n => n.NeighbourTerritoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
