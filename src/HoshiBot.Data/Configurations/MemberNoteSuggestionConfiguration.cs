using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class MemberNoteSuggestionConfiguration : IEntityTypeConfiguration<MemberNoteSuggestion>
{
    public void Configure(EntityTypeBuilder<MemberNoteSuggestion> builder)
    {
        builder.HasKey(s => s.Id);

        // The review queue is filtered by guild + Pending.
        builder.HasIndex(s => new { s.GuildId, s.Status });

        builder.HasOne(s => s.Guild)
            .WithMany()
            .HasForeignKey(s => s.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        // Provenance only — keep the suggestion (for the transcript-less review) if the interview
        // it came from is deleted.
        builder.HasOne(s => s.SourceInterview)
            .WithMany()
            .HasForeignKey(s => s.SourceInterviewId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
