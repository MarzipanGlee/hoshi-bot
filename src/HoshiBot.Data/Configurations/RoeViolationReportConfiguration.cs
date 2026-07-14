using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class RoeViolationReportConfiguration : IEntityTypeConfiguration<RoeViolationReport>
{
    public void Configure(EntityTypeBuilder<RoeViolationReport> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.GuildId);

        builder.HasOne(r => r.Guild)
            .WithMany()
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Cascade — a historical report should survive its alliance being unlinked.
        builder.HasOne(r => r.GuildAlliance)
            .WithMany()
            .HasForeignKey(r => r.GuildAllianceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
