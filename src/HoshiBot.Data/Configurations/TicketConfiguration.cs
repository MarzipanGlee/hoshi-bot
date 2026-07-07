using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Subject).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => t.GuildId);

        builder.HasOne(t => t.Guild)
            .WithMany()
            .HasForeignKey(t => t.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
