using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ConditionalRoleConditionConfiguration : IEntityTypeConfiguration<ConditionalRoleCondition>
{
    public void Configure(EntityTypeBuilder<ConditionalRoleCondition> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();

        // Names are what an admin picks a condition by in the rule editor, so two identically named
        // conditions in one guild would be indistinguishable there.
        builder.HasIndex(c => new { c.GuildId, c.Name }).IsUnique();

        builder.HasOne(c => c.Guild)
            .WithMany()
            .HasForeignKey(c => c.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
