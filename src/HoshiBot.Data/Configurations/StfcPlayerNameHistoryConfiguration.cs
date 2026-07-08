using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcPlayerNameHistoryConfiguration : IEntityTypeConfiguration<StfcPlayerNameHistory>
{
    public void Configure(EntityTypeBuilder<StfcPlayerNameHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Name).HasMaxLength(100).IsRequired();

        // Not unique on (StfcPlayerId, Name) — a player can switch back to a name they
        // used before (A -> B -> A again), which is a legitimate second row at a later
        // ObservedAt, not a duplicate. "Don't insert if unchanged since the last sync" is
        // an application-level check (compare against the most recent row by ObservedAt),
        // not something a uniqueness constraint can express correctly here.
        builder.HasIndex(h => h.StfcPlayerId);

        builder.HasOne(h => h.StfcPlayer)
            .WithMany(p => p.NameHistory)
            .HasForeignKey(h => h.StfcPlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
