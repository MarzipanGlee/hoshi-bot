using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class AiChatProviderHealthConfiguration : IEntityTypeConfiguration<AiChatProviderHealth>
{
    public void Configure(EntityTypeBuilder<AiChatProviderHealth> builder)
    {
        builder.HasKey(h => h.Id);

        // One row per (guild, call kind) — the upsert key.
        builder.HasIndex(h => new { h.GuildId, h.Kind }).IsUnique();

        // Provider messages (stack-trace-ish) can be long; cap so a row can't bloat.
        builder.Property(h => h.LastErrorMessage).HasMaxLength(1000);

        builder.HasOne(h => h.Guild)
            .WithMany()
            .HasForeignKey(h => h.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
