using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcServerDiscordInviteConfiguration : IEntityTypeConfiguration<StfcServerDiscordInvite>
{
    public void Configure(EntityTypeBuilder<StfcServerDiscordInvite> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Url).HasMaxLength(300).IsRequired();
        builder.HasIndex(i => i.ServerId);

        builder.HasOne(i => i.Server)
            .WithMany(s => s.DiscordInvites)
            .HasForeignKey(i => i.ServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
