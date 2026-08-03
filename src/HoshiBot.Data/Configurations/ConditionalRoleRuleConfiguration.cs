using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ConditionalRoleRuleConfiguration : IEntityTypeConfiguration<ConditionalRoleRule>
{
    public void Configure(EntityTypeBuilder<ConditionalRoleRule> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();

        // The sync loads every rule of one guild; no uniqueness, since several rules may target the
        // same role (a member holds it if any of them matches).
        builder.HasIndex(r => r.GuildId);

        builder.HasOne(r => r.Guild)
            .WithMany()
            .HasForeignKey(r => r.GuildId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
