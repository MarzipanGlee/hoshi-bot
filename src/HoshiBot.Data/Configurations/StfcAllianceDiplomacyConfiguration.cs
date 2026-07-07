using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcAllianceDiplomacyConfiguration : IEntityTypeConfiguration<StfcAllianceDiplomacy>
{
    public void Configure(EntityTypeBuilder<StfcAllianceDiplomacy> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => new { d.SourceAllianceId, d.TargetAllianceId }).IsUnique();

        builder.HasOne(d => d.SourceAlliance)
            .WithMany()
            .HasForeignKey(d => d.SourceAllianceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.TargetAlliance)
            .WithMany()
            .HasForeignKey(d => d.TargetAllianceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
