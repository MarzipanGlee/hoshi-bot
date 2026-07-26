using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HoshiBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiChatEmbeddingHnswIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AiChatIndexedMessages_Embedding",
                table: "AiChatIndexedMessages",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            // The vector query filters by GuildId/EmbeddingModel; a global HNSW index returns the
            // global top-k by distance and only THEN applies those filters, which would starve a
            // small guild's results (its rows aren't in the global top-k). pgvector 0.8's iterative
            // scan keeps scanning until the LIMIT is filled after filtering. ef_search gives recall
            // headroom above the LIMIT-40 candidate pool. Set at the database level so every
            // connection inherits it (hnsw.* is a dotted custom GUC class, so ALTER DATABASE SET is
            // accepted even before the extension library loads in this session). DB name is fixed
            // across the project (compose + HoshiBotDbContextFactory default).
            migrationBuilder.Sql("ALTER DATABASE hoshibot SET hnsw.iterative_scan = 'relaxed_order';");
            migrationBuilder.Sql("ALTER DATABASE hoshibot SET hnsw.ef_search = 100;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER DATABASE hoshibot RESET hnsw.ef_search;");
            migrationBuilder.Sql("ALTER DATABASE hoshibot RESET hnsw.iterative_scan;");

            migrationBuilder.DropIndex(
                name: "IX_AiChatIndexedMessages_Embedding",
                table: "AiChatIndexedMessages");
        }
    }
}
