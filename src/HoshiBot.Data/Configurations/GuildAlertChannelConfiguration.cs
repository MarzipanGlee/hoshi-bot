using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildAlertChannelConfiguration : IEntityTypeConfiguration<GuildAlertChannel>
{
    public void Configure(EntityTypeBuilder<GuildAlertChannel> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.GuildId);

        builder.HasOne(c => c.Guild)
            .WithMany()
            .HasForeignKey(c => c.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
