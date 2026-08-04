using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcPlayerConfiguration : IEntityTypeConfiguration<StfcPlayer>
{
    public void Configure(EntityTypeBuilder<StfcPlayer> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();

        // Wider than Name: a single character can expand (ы → "bi", Ю → "io", ß → "ss"), so a
        // 100-character name of nothing but those would overflow a matching limit.
        builder.Property(p => p.NameKey).HasMaxLength(200);

        // The searches filter on this, and the auto-link matcher looks up an exact key. Thousands of
        // rows rather than millions, so a plain btree is enough — the LIKE searches will scan, which
        // at this size costs less than the round trip.
        builder.HasIndex(p => p.NameKey);

        // ExternalId, not (ServerId, Name) — a player's Name is just their latest known
        // name and can legitimately change (see StfcPlayerNameHistory), so it can't be
        // part of a uniqueness constraint; ExternalId is the stable identity instead.
        builder.HasIndex(p => p.ExternalId).IsUnique();

        builder.HasOne(p => p.Server)
            .WithMany(s => s.Players)
            .HasForeignKey(p => p.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Alliance)
            .WithMany(a => a.Players)
            .HasForeignKey(p => p.AllianceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
