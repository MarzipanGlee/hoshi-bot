using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcVeilGroupConfiguration : IEntityTypeConfiguration<StfcVeilGroup>
{
    public void Configure(EntityTypeBuilder<StfcVeilGroup> builder)
    {
        builder.HasKey(v => v.Id);
        // Ids match Scopely's own veil-group numbering (US-1=1 ... APAC-6=6), not an EF
        // sequence — assigned explicitly by the seeder/admin, never auto-generated.
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(v => new { v.RegionId, v.Name }).IsUnique();

        builder.HasOne(v => v.Region)
            .WithMany(r => r.VeilGroups)
            .HasForeignKey(v => v.RegionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
