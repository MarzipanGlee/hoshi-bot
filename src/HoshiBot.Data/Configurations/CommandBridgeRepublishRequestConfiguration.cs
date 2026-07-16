using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class CommandBridgeRepublishRequestConfiguration : IEntityTypeConfiguration<CommandBridgeRepublishRequest>
{
    public void Configure(EntityTypeBuilder<CommandBridgeRepublishRequest> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.RequestedAt);

        builder.HasOne(r => r.Guild)
            .WithMany(g => g.CommandBridgeRepublishRequests)
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
