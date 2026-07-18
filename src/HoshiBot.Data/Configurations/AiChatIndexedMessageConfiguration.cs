using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class AiChatIndexedMessageConfiguration : IEntityTypeConfiguration<AiChatIndexedMessage>
{
    public void Configure(EntityTypeBuilder<AiChatIndexedMessage> builder)
    {
        builder.HasKey(m => m.Id);

        // The Discord message id is the upsert key — one row per message.
        builder.HasIndex(m => m.MessageId).IsUnique();

        // Every search scopes by guild first, so index it.
        builder.HasIndex(m => m.GuildId);

        // Fixed 768-dim embedding (matches the default embeddinggemma model). No ANN index for v1
        // — a sequential cosine scan is fine at per-guild knowledge scale (mirrors the no-GIN
        // decision for the FTS side); revisit with HNSW/IVFFlat if a guild's index grows large.
        builder.Property(m => m.Embedding).HasColumnType("vector(768)");

        builder.HasOne(m => m.Guild)
            .WithMany()
            .HasForeignKey(m => m.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
