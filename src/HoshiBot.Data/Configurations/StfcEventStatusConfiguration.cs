using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcEventStatusConfiguration : IEntityTypeConfiguration<StfcEventStatus>
{
    public void Configure(EntityTypeBuilder<StfcEventStatus> builder)
    {
        builder.HasKey(e => e.EventGroup);
        builder.Property(e => e.EventGroup).HasMaxLength(50);
    }
}
