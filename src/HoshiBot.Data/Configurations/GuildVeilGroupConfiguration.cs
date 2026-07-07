using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildVeilGroupConfiguration : IEntityTypeConfiguration<GuildVeilGroup>
{
    public void Configure(EntityTypeBuilder<GuildVeilGroup> builder)
    {
        builder.HasKey(gv => gv.Id);
        builder.HasIndex(gv => new { gv.GuildId, gv.StfcVeilGroupId }).IsUnique();

        builder.HasOne(gv => gv.Guild)
            .WithMany(g => g.VeilGroupLinks)
            .HasForeignKey(gv => gv.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gv => gv.StfcVeilGroup)
            .WithMany()
            .HasForeignKey(gv => gv.StfcVeilGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
