using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcSystemConfiguration : IEntityTypeConfiguration<StfcSystem>
{
    public void Configure(EntityTypeBuilder<StfcSystem> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.Number).IsUnique();

        builder.HasOne(s => s.Territory)
            .WithMany(t => t.Systems)
            .HasForeignKey(s => s.TerritoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
