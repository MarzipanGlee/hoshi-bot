using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ThreadRemovalRequestConfiguration : IEntityTypeConfiguration<ThreadRemovalRequest>
{
    public void Configure(EntityTypeBuilder<ThreadRemovalRequest> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.RequestedAt);

        builder.HasOne(r => r.Guild)
            .WithMany(g => g.ThreadRemovalRequests)
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
