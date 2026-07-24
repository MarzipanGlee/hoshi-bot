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

        // Fixed 768-dim embedding (matches the default embeddinggemma model).
        builder.Property(m => m.Embedding).HasColumnType("vector(768)");

        // HNSW ANN index for the semantic leg: the sequential cosine scan hit ~1.4s over ~39k rows
        // (measured), and it runs on every embeddings-enabled answer. vector_cosine_ops matches the
        // <=> / CosineDistance the query uses. Note: with the per-guild GuildId/EmbeddingModel filters,
        // a global HNSW index returns the global top-k then filters — which would starve a small guild's
        // results; the AddAiChatEmbeddingHnswIndex migration enables pgvector 0.8's
        // hnsw.iterative_scan=relaxed_order (DB-level) so filtered search still fills its candidate pool.
        builder.HasIndex(m => m.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        builder.HasOne(m => m.Guild)
            .WithMany()
            .HasForeignKey(m => m.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
