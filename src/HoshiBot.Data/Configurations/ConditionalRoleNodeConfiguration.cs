using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class ConditionalRoleNodeConfiguration : IEntityTypeConfiguration<ConditionalRoleNode>
{
    public void Configure(EntityTypeBuilder<ConditionalRoleNode> builder)
    {
        builder.HasKey(n => n.Id);

        // The two read paths: load one owner's whole tree.
        builder.HasIndex(n => n.OwnerRuleId);
        builder.HasIndex(n => n.OwnerConditionId);

        // Exactly one owner. Enforced in the database as well as in the service, because a node with
        // neither owner is unreachable garbage and one with both would be evaluated twice.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ConditionalRoleNodes_SingleOwner",
            """("OwnerRuleId" IS NULL) <> ("OwnerConditionId" IS NULL)"""));

        // Cascade from whichever owner the node has — deleting a rule or a condition takes its tree
        // with it. A row only ever has one of these set, so the two paths never both reach it and
        // Postgres has no multiple-cascade-path to reject.
        builder.HasOne(n => n.OwnerRule)
            .WithMany(r => r.Nodes)
            .HasForeignKey(n => n.OwnerRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.OwnerCondition)
            .WithMany(c => c.Nodes)
            .HasForeignKey(n => n.OwnerConditionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: a tree is always rewritten by deleting every node of the owner in
        // one statement, so children never need the database to chase them, and Restrict keeps a
        // self-referencing cascade out of the schema.
        builder.HasOne(n => n.Parent)
            .WithMany(n => n.Children)
            .HasForeignKey(n => n.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict on purpose: deleting a condition another tree still references must fail loudly
        // so the editor can say which rules use it, rather than silently gutting those rules.
        builder.HasOne(n => n.ReferencedCondition)
            .WithMany()
            .HasForeignKey(n => n.ReferencedConditionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
