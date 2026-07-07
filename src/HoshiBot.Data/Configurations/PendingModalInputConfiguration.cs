using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class PendingModalInputConfiguration : IEntityTypeConfiguration<PendingModalInput>
{
    public void Configure(EntityTypeBuilder<PendingModalInput> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => p.GuildId);

        builder.HasOne(p => p.Guild)
            .WithMany()
            .HasForeignKey(p => p.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
