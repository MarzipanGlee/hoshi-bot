using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcNewsPostGuildMessageConfiguration : IEntityTypeConfiguration<StfcNewsPostGuildMessage>
{
    public void Configure(EntityTypeBuilder<StfcNewsPostGuildMessage> builder)
    {
        builder.HasIndex(m => new { m.StfcNewsPostId, m.GuildId }).IsUnique();

        builder.HasOne(m => m.StfcNewsPost)
            .WithMany(p => p.GuildMessages)
            .HasForeignKey(m => m.StfcNewsPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
