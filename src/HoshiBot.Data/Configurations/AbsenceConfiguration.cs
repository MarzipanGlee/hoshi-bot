using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class AbsenceConfiguration : IEntityTypeConfiguration<Absence>
{
    public void Configure(EntityTypeBuilder<Absence> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Reason).HasMaxLength(200);
        builder.HasIndex(a => new { a.GuildId, a.DiscordUserId, a.EndsAt });

        builder.HasOne(a => a.GuildMember)
            .WithMany()
            .HasForeignKey(a => new { a.GuildId, a.DiscordUserId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.EditsAbsence)
            .WithMany()
            .HasForeignKey(a => a.EditsAbsenceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
