using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildAllianceConfiguration : IEntityTypeConfiguration<GuildAlliance>
{
    public void Configure(EntityTypeBuilder<GuildAlliance> builder)
    {
        builder.HasKey(ga => ga.Id);
        builder.HasIndex(ga => new { ga.GuildId, ga.StfcAllianceId }).IsUnique();
        builder.Property(ga => ga.Language).HasMaxLength(10);

        builder.HasOne(ga => ga.Guild)
            .WithMany(g => g.AllianceLinks)
            .HasForeignKey(ga => ga.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ga => ga.StfcAlliance)
            .WithMany()
            .HasForeignKey(ga => ga.StfcAllianceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
