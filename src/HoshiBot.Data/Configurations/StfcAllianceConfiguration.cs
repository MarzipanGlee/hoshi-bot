using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcAllianceConfiguration : IEntityTypeConfiguration<StfcAlliance>
{
    public void Configure(EntityTypeBuilder<StfcAlliance> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Tag).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(a => new { a.ServerId, a.Tag }).IsUnique();

        builder.HasOne(a => a.Server)
            .WithMany(s => s.Alliances)
            .HasForeignKey(a => a.ServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
