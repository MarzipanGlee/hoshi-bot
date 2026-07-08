using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ChannelPermissionExpectationConfiguration : IEntityTypeConfiguration<ChannelPermissionExpectation>
{
    public void Configure(EntityTypeBuilder<ChannelPermissionExpectation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.GuildId, e.ChannelId, e.RoleId }).IsUnique();

        builder.HasOne(e => e.Guild)
            .WithMany(g => g.ChannelPermissionExpectations)
            .HasForeignKey(e => e.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
