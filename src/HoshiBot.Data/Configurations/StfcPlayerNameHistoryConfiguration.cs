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

        // A resync re-observing the same current name shouldn't create a duplicate row —
        // this is the safety net behind that, not just an application-logic check.
        builder.HasIndex(h => new { h.StfcPlayerId, h.Name }).IsUnique();

        builder.HasOne(h => h.StfcPlayer)
            .WithMany(p => p.NameHistory)
            .HasForeignKey(h => h.StfcPlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
