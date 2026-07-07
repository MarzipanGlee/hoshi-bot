using HoshiBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoshiBot.Data.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).HasMaxLength(256).IsRequired();
        builder.HasIndex(a => a.GuildId);

        builder.Property(a => a.AttachmentUrls)
            .HasConversion(
                v => string.Join('\n', v),
                v => v.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                v => v.ToArray()));

        builder.HasOne(a => a.Guild)
            .WithMany()
            .HasForeignKey(a => a.GuildId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.ReadReceipts)
            .WithOne(r => r.Announcement)
            .HasForeignKey(r => r.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
