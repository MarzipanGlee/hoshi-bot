using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class GuildMemoryConfiguration : IEntityTypeConfiguration<GuildMemory>
{
    public void Configure(EntityTypeBuilder<GuildMemory> builder)
    {
        builder.HasKey(m => m.Id);

        // Recall/consolidation always scope by guild + scope first.
        builder.HasIndex(m => new { m.GuildId, m.Scope });

        // Member-scoped recall looks memories up by the person key (Phase 3).
        builder.HasIndex(m => new { m.GuildId, m.SubjectPersonKey });

        // Fixed 768-dim embedding (embeddinggemma), same as the knowledge index. No ANN index yet —
        // a sequential cosine scan is fine at current memory scale (a handful of rows per guild).
        // When it grows large, copy the knowledge index's HNSW setup (see
        // AiChatIndexedMessageConfiguration + the AddAiChatEmbeddingHnswIndex migration).
        builder.Property(m => m.Embedding).HasColumnType("vector(768)");

        builder.HasOne(m => m.Guild)
            .WithMany()
            .HasForeignKey(m => m.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
