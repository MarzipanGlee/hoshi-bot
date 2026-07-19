using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class MemberInterviewConfiguration : IEntityTypeConfiguration<MemberInterview>
{
    public void Configure(EntityTypeBuilder<MemberInterview> builder)
    {
        builder.HasKey(i => i.Id);

        // One interview per member per guild — the invite job dedups on this.
        builder.HasIndex(i => new { i.GuildId, i.DiscordUserId }).IsUnique();

        builder.HasOne(i => i.Guild)
            .WithMany()
            .HasForeignKey(i => i.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Messages)
            .WithOne(m => m.Interview)
            .HasForeignKey(m => m.InterviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
