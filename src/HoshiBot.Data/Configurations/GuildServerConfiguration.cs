using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildServerConfiguration : IEntityTypeConfiguration<GuildServer>
{
    public void Configure(EntityTypeBuilder<GuildServer> builder)
    {
        builder.HasKey(gs => gs.Id);
        builder.HasIndex(gs => new { gs.GuildId, gs.StfcServerId }).IsUnique();

        builder.HasOne(gs => gs.Guild)
            .WithMany(g => g.ServerLinks)
            .HasForeignKey(gs => gs.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gs => gs.StfcServer)
            .WithMany()
            .HasForeignKey(gs => gs.StfcServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
