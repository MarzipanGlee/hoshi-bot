using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcClientReleaseConfiguration : IEntityTypeConfiguration<StfcClientRelease>
{
    public void Configure(EntityTypeBuilder<StfcClientRelease> builder)
    {
        builder.HasKey(r => r.Platform);
        builder.Property(r => r.Version).HasMaxLength(32);
        builder.Property(r => r.NotifiedVersion).HasMaxLength(32);
    }
}
