using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcAllianceDiscordInviteConfiguration : IEntityTypeConfiguration<StfcAllianceDiscordInvite>
{
    public void Configure(EntityTypeBuilder<StfcAllianceDiscordInvite> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Url).HasMaxLength(300).IsRequired();
        builder.HasIndex(i => i.AllianceId);

        builder.HasOne(i => i.Alliance)
            .WithMany(a => a.DiscordInvites)
            .HasForeignKey(i => i.AllianceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
