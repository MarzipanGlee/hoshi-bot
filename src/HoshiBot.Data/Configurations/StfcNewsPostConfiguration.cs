using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcNewsPostConfiguration : IEntityTypeConfiguration<StfcNewsPost>
{
    public void Configure(EntityTypeBuilder<StfcNewsPost> builder)
    {
        builder.Property(p => p.Link).HasMaxLength(500).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(300).IsRequired();
        builder.Property(p => p.EventGroup).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => p.Link).IsUnique();
    }
}
