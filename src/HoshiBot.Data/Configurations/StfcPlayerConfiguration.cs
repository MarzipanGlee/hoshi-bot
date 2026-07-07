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
        builder.HasIndex(p => new { p.ServerId, p.Name }).IsUnique();

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
