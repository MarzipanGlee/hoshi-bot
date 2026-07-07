using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class StfcVeilGroupDiscordInviteConfiguration : IEntityTypeConfiguration<StfcVeilGroupDiscordInvite>
{
    public void Configure(EntityTypeBuilder<StfcVeilGroupDiscordInvite> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Url).HasMaxLength(300).IsRequired();
        builder.HasIndex(i => i.VeilGroupId);

        builder.HasOne(i => i.VeilGroup)
            .WithMany(v => v.DiscordInvites)
            .HasForeignKey(i => i.VeilGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
