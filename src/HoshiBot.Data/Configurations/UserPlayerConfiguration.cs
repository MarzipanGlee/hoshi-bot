using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class UserPlayerConfiguration : IEntityTypeConfiguration<UserPlayer>
{
    public void Configure(EntityTypeBuilder<UserPlayer> builder)
    {
        builder.HasKey(up => up.Id);
        builder.HasIndex(up => new { up.DiscordUserId, up.StfcPlayerId }).IsUnique();

        builder.HasOne(up => up.User)
            .WithMany(u => u.PlayerLinks)
            .HasForeignKey(up => up.DiscordUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(up => up.StfcPlayer)
            .WithMany(p => p.UserLinks)
            .HasForeignKey(up => up.StfcPlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
