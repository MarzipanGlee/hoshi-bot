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

        builder.HasOne(m => m.Guild)
            .WithMany()
            .HasForeignKey(m => m.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
