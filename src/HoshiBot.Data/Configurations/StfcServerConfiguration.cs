using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcServerConfiguration : IEntityTypeConfiguration<StfcServer>
{
    public void Configure(EntityTypeBuilder<StfcServer> builder)
    {
        builder.HasKey(s => s.Id);
        // Id is the real Scopely server number (e.g. "US 8") — assigned explicitly by the
        // seeder/admin, never auto-generated. The PK is already globally unique, so no
        // separate unique index is needed here (unlike when it was a plain Number column).
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(50).IsRequired();

        // Server names aren't globally unique in STFC — the same thematic name can appear
        // in more than one veil group (e.g. real data has a "Tanagra" in both EU-4 and
        // APAC-6), so uniqueness is scoped per veil group instead.
        builder.HasIndex(s => new { s.VeilGroupId, s.Name }).IsUnique();

        builder.HasOne(s => s.Region)
            .WithMany(r => r.Servers)
            .HasForeignKey(s => s.RegionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.VeilGroup)
            .WithMany(v => v.Servers)
            .HasForeignKey(s => s.VeilGroupId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
