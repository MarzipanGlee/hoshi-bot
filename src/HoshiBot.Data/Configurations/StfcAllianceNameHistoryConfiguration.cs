using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcAllianceNameHistoryConfiguration : IEntityTypeConfiguration<StfcAllianceNameHistory>
{
    public void Configure(EntityTypeBuilder<StfcAllianceNameHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Tag).HasMaxLength(20).IsRequired();
        builder.Property(h => h.Name).HasMaxLength(100).IsRequired();

        // Not unique on (StfcAllianceId, Tag, Name) — same reasoning as
        // StfcPlayerNameHistory: an alliance can revert to a Tag/Name it used before.
        builder.HasIndex(h => h.StfcAllianceId);

        builder.HasOne(h => h.StfcAlliance)
            .WithMany(a => a.NameHistory)
            .HasForeignKey(h => h.StfcAllianceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
