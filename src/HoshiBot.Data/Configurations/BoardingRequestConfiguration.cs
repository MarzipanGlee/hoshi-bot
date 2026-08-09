using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class BoardingRequestConfiguration : IEntityTypeConfiguration<BoardingRequest>
{
    public void Configure(EntityTypeBuilder<BoardingRequest> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.RequestedAt);
        builder.Property(r => r.LastError).HasMaxLength(500);

        builder.HasOne(r => r.Guild)
            .WithMany()
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.GuildAlliance)
            .WithMany()
            .HasForeignKey(r => r.GuildAllianceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
