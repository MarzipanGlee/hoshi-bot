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
